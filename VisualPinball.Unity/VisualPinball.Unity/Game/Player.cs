// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Trigger;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class Player : MonoBehaviour
	{
		public TableApi TableApi { get; }
		public PlayfieldApi PlayfieldApi { get; private set; }

		// shortcuts
		public GameObject Playfield => _playfieldComponent.gameObject;

		[NonSerialized]
		public IGamelogicEngine GamelogicEngine;

		[NonSerialized]
		public BallManager BallManager;

		[HideInInspector] [SerializeField] public string debugUiId;
		[HideInInspector] [SerializeField] public string physicsEngineId;

		// table related
		private TableContainer _tableContainer;
		private readonly List<IApi> _apis = new List<IApi>();
		private readonly List<IApiInitializable> _initializables = new List<IApiInitializable>();
		private readonly List<IApiColliderGenerator> _colliderGenerators = new List<IApiColliderGenerator>();
		private readonly Dictionary<Entity, IApiHittable> _hittables = new Dictionary<Entity, IApiHittable>();
		private readonly Dictionary<Entity, IApiRotatable> _rotatables = new Dictionary<Entity, IApiRotatable>();
		private readonly Dictionary<Entity, IApiCollidable> _collidables = new Dictionary<Entity, IApiCollidable>();
		private readonly Dictionary<Entity, IApiSpinnable> _spinnables = new Dictionary<Entity, IApiSpinnable>();
		private readonly Dictionary<Entity, IApiSlingshot> _slingshots = new Dictionary<Entity, IApiSlingshot>();

		internal readonly Dictionary<Entity, Transform> FlipperTransforms = new Dictionary<Entity, Transform>();
		internal readonly Dictionary<Entity, Transform> BumperSkirtTransforms = new Dictionary<Entity, Transform>();
		internal readonly Dictionary<Entity, Transform> BumperRingTransforms = new Dictionary<Entity, Transform>();
		internal readonly Dictionary<Entity, Transform> GateWireTransforms = new Dictionary<Entity, Transform>();
		internal readonly Dictionary<Entity, Transform> HitTargetTransforms = new Dictionary<Entity, Transform>();
		internal readonly Dictionary<Entity, Transform> DropTargetTransforms = new Dictionary<Entity, Transform>();
		internal readonly Dictionary<Entity, Transform> SpinnerPlateTransforms = new Dictionary<Entity, Transform>();
		internal readonly Dictionary<Entity, Transform> TriggerTransforms = new Dictionary<Entity, Transform>();
		internal readonly Dictionary<Entity, SkinnedMeshRenderer[]> PlungerSkinnedMeshRenderers = new Dictionary<Entity, SkinnedMeshRenderer[]>();
		internal readonly Dictionary<Entity, GameObject> Balls = new Dictionary<Entity, GameObject>();

		internal IEnumerable<IApiColliderGenerator> ColliderGenerators => _colliderGenerators;

		// input related
		private InputManager _inputManager;
		private VisualPinballSimulationSystemGroup _simulationSystemGroup;
		[NonSerialized] private readonly LampPlayer _lampPlayer = new LampPlayer();
		[NonSerialized] private readonly CoilPlayer _coilPlayer = new CoilPlayer();
		[NonSerialized] private readonly SwitchPlayer _switchPlayer = new SwitchPlayer();
		[NonSerialized] private readonly WirePlayer _wirePlayer = new WirePlayer();
		[NonSerialized] private readonly DisplayPlayer _displayPlayer = new DisplayPlayer();
		[NonSerialized] private readonly List<(InputAction, Action<InputAction.CallbackContext>)> _actions = new List<(InputAction, Action<InputAction.CallbackContext>)>();

		private const float SlowMotionMax = 0.1f;
		private const float TimeLapseMax = 2.5f;

		internal static readonly Entity PlayfieldEntity = new Entity {Index = -3, Version = 0}; // a fake entity we just use for reference

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private TableAuthoring _tableComponent;
		private PlayfieldAuthoring _playfieldComponent;

		#region Access

		internal IApiSwitch Switch(ISwitchDeviceAuthoring component, string switchItem) => component != null ? _switchPlayer.Switch(component, switchItem) : null;
		internal IApiCoil Coil(ICoilDeviceAuthoring component, string coilItem) => component != null ? _coilPlayer.Coil(component, coilItem) : null;
		internal IApiWireDeviceDest WireDevice(IWireableAuthoring c) => _wirePlayer.WireDevice(c);
		public Dictionary<string, bool> SwitchStatusesClosed => _switchPlayer.SwitchStatusesClosed;
		public Dictionary<string, bool> CoilStatuses => _coilPlayer.CoilStatuses;
		public Dictionary<string, float> LampStatuses => _lampPlayer.LampStatuses;
		public float3 Gravity => _playfieldComponent.Gravity;

		#endregion

		#region Lifecycle

		public Player()
		{
			TableApi = new TableApi(this);
		}

		private void Awake()
		{
			_tableComponent = GetComponent<TableAuthoring>();
			_playfieldComponent = GetComponentInChildren<PlayfieldAuthoring>();
			var engineComponent = GetComponent<IGamelogicEngine>();

			_tableComponent.TableContainer.Refresh();

			_initializables.Add(TableApi);

			_tableContainer = _tableComponent.TableContainer;
			BallManager = new BallManager(this);
			_inputManager = new InputManager();
			_inputManager.Enable(HandleInput);

			if (engineComponent != null) {
				GamelogicEngine = engineComponent;
				_lampPlayer.Awake(_tableComponent, GamelogicEngine);
				_coilPlayer.Awake(_tableComponent, GamelogicEngine, _lampPlayer);
				_switchPlayer.Awake(_tableComponent, GamelogicEngine, _inputManager);
				_wirePlayer.Awake(_tableComponent, _inputManager, _switchPlayer);
				_displayPlayer.Awake(GamelogicEngine);
			}

			EngineProvider<IPhysicsEngine>.Set(physicsEngineId);
			EngineProvider<IPhysicsEngine>.Get().Init(_tableComponent, BallManager);
			if (!string.IsNullOrEmpty(debugUiId)) {
				EngineProvider<IDebugUI>.Set(debugUiId);
			}
			_simulationSystemGroup = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<VisualPinballSimulationSystemGroup>();
		}

		private void Start()
		{
			// trigger init events now
			foreach (var i in _initializables) {
				i.OnInit(BallManager);
			}

			_coilPlayer.OnStart();
			_switchPlayer.OnStart();
			_lampPlayer.OnStart();
			_wirePlayer.OnStart();

			GamelogicEngine?.OnInit(this, TableApi, BallManager);
		}

		private void OnDestroy()
		{
			foreach (var i in _apis) {
				i.OnDestroy();
			}

			_inputManager.Disable(HandleInput);
			_coilPlayer.OnDestroy();
			_switchPlayer.OnDestroy();
			_lampPlayer.OnDestroy();
			_wirePlayer.OnDestroy();
			_displayPlayer.OnDestroy();

			foreach (var (action, callback) in _actions) {
				action.performed -= callback;
			}
		}

		#endregion

		#region Registrations

		public void RegisterBumper(BumperAuthoring component, Entity entity, Entity parentEntity)
		{
			Register(TableApi.Bumpers, new BumperApi(component.gameObject, entity, parentEntity, this), component, entity);
			RegisterTransform<BumperRingAnimationAuthoring>(BumperRingTransforms, component, entity);
			RegisterTransform<BumperSkirtAnimationAuthoring>(BumperSkirtTransforms, component, entity);
		}

		public void RegisterFlipper(FlipperAuthoring component, Entity entity, Entity parentEntity)
		{
			Register(TableApi.Flippers, new FlipperApi(component.gameObject, entity, parentEntity, this), component, entity);
			FlipperTransforms[entity] = component.gameObject.transform;

			if (EngineProvider<IDebugUI>.Exists) {
				EngineProvider<IDebugUI>.Get().OnRegisterFlipper(entity, component.gameObject.name);
			}
		}

		public void RegisterGate(GateAuthoring component, Entity entity, Entity parentEntity)
		{
			Register(TableApi.Gates, new GateApi(component.gameObject, entity, parentEntity, this), component, entity);
			RegisterTransform<GateWireAnimationAuthoring>(GateWireTransforms, component, entity);
		}

		public void RegisterHitTarget(TargetAuthoring component, Entity entity, Entity parentEntity)
		{
			Register(TableApi.HitTargets, new HitTargetApi(component.gameObject, entity, parentEntity, this), component, entity);
			RegisterTransform<HitTargetAnimationAuthoring>(HitTargetTransforms, component, entity);
			RegisterTransform<DropTargetAnimationAuthoring>(DropTargetTransforms, component, entity);
		}

		public void RegisterKicker(KickerAuthoring component, Entity entity, Entity parentEntity)
		{
			Register(TableApi.Kickers, new KickerApi(component.gameObject, entity, parentEntity, this), component, entity);
		}

		public void RegisterLamp(LightAuthoring component)
		{
			Register(TableApi.Lights, new LightApi(component.gameObject, this), component);
		}

		public void RegisterPlunger(PlungerAuthoring component, Entity entity, Entity parentEntity, InputActionReference actionRef)
		{
			var plungerApi = new PlungerApi(component.gameObject, entity, parentEntity, this);
			Register(TableApi.Plungers, plungerApi, component, entity);

			if (actionRef != null) {
				actionRef.action.performed += plungerApi.OnAnalogPlunge;
				_actions.Add((actionRef.action, plungerApi.OnAnalogPlunge));
			}

			PlungerSkinnedMeshRenderers[entity] = component.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
		}

		public void RegisterPlayfield(GameObject go)
		{
			PlayfieldApi = new PlayfieldApi(go, this);
			_colliderGenerators.Add(PlayfieldApi);
		}

		public void RegisterPrimitive(PrimitiveAuthoring component, Entity entity, Entity parentEntity)
		{
			Register(TableApi.Primitives, new PrimitiveApi(component.gameObject, entity, parentEntity, this), component, entity);
		}

		public void RegisterRamp(RampAuthoring component, Entity entity, Entity parentEntity)
		{
			Register(TableApi.Ramps, new RampApi(component.gameObject, entity, parentEntity, this), component, entity);
		}

		public void RegisterRubber(RubberAuthoring component, Entity entity, Entity parentEntity)
		{
			Register(TableApi.Rubbers, new RubberApi(component.gameObject, entity, parentEntity, this), component, entity);
		}

		public void RegisterSpinner(SpinnerAuthoring component, Entity entity, Entity parentEntity)
		{
			Register(TableApi.Spinners, new SpinnerApi(component.gameObject, entity, parentEntity, this), component, entity);
			RegisterTransform<SpinnerPlateAnimationAuthoring>(SpinnerPlateTransforms, component, entity);
		}

		public void RegisterSurface(SurfaceAuthoring component, Entity entity, Entity parentEntity)
		{
			Register(TableApi.Surfaces, new SurfaceApi(component.gameObject, entity, parentEntity, this), component, entity);
		}

		public void RegisterTrigger(TriggerAuthoring component, Entity entity, Entity parentEntity)
		{
			Register(TableApi.Triggers, new TriggerApi(component.gameObject, entity, parentEntity, this), component, entity);
			TriggerTransforms[entity] = component.gameObject.transform;
		}

		public void RegisterTrigger(TriggerData data, Entity entity, GameObject go)
		{
			var component = go.AddComponent<TriggerAuthoring>();
			component.SetData(data);
			Register(TableApi.Triggers, new TriggerApi(go, entity, Entity.Null, this), component, entity);
		}

		public void RegisterTrough(TroughAuthoring component)
		{
			Register(TableApi.Troughs, new TroughApi(component.gameObject, this), component);
		}

		private void Register<TApi>(Dictionary<string, TApi> apis, TApi api, MonoBehaviour component, Entity entity = default) where TApi : IApi
		{
			apis[api.Name] = api;
			_apis.Add(api);
			if (api is IApiInitializable initializable) {
				_initializables.Add(initializable);
			}
			if (api is IApiRotatable rotatable) {
				_rotatables[entity] = rotatable;
			}
			if (api is IApiSlingshot slingshot) {
				_slingshots[entity] = slingshot;
			}
			if (api is IApiSwitchDevice switchDevice) {
				if (component is ISwitchDeviceAuthoring switchDeviceAuthoring) {
					_switchPlayer.RegisterSwitchDevice(switchDeviceAuthoring, switchDevice);
				} else  {
					Logger.Warn($"{component.GetType()} is not of type ISwitchDeviceAuthoring while ${api.GetType()} is of type IApiSwitchDevice.");
				}
			}
			if (api is IApiCoilDevice coilDevice) {
				if (component is ICoilDeviceAuthoring coilDeviceAuthoring) {
					_coilPlayer.RegisterCoilDevice(coilDeviceAuthoring, coilDevice);
				} else {
					Logger.Warn($"{component.GetType()} is not of type ICoilDeviceAuthoring while ${api.GetType()} is of type IApiCoilDevice.");
				}
			}
			if (api is IApiWireDeviceDest wireDevice) {
				if (component is IWireableAuthoring wireableDeviceAuthoring) {
					_wirePlayer.RegisterWireDevice(wireableDeviceAuthoring, wireDevice);
				} else {
					Logger.Warn($"{component.GetType()} is not of type ICoilDeviceAuthoring while ${api.GetType()} is of type IApiWireDeviceDest.");
				}
			}

			if (api is IApiLamp lamp) {
				if (component is ILampDeviceAuthoring lampAuthoring) {
					_lampPlayer.RegisterLamp(lampAuthoring, lamp);
				} else {
					Logger.Warn($"{component.GetType()} is not of type ILampAuthoring while ${api.GetType()} is of type IApiLamp.");
				}
			}

			if (api is IApiColliderGenerator colliderGenerator) {
				RegisterCollider(entity, colliderGenerator);
			}
		}

		private void RegisterTransform<T>(Dictionary<Entity, Transform> transforms, MonoBehaviour component, Entity entity) where T : MonoBehaviour
		{
			var comp = component.gameObject.GetComponentInChildren<T>();
			if (comp) {
				transforms[entity] = comp.gameObject.transform;
			}
		}

		private void RegisterCollider(Entity entity, IApiColliderGenerator apiColl)
		{
			if (!apiColl.IsColliderAvailable) {
				return;
			}
			_colliderGenerators.Add(apiColl);
			if (apiColl is IApiHittable apiHittable) {
				_hittables[entity] = apiHittable;
			}

			if (apiColl is IApiCollidable apiCollidable) {
				_collidables[entity] = apiCollidable;
			}
		}

		#endregion

		#region Events

		public void Queue(Action action) => _simulationSystemGroup.QueueBeforeBallCreation(action);
		public void ScheduleAction(int timeMs, Action action) => _simulationSystemGroup.ScheduleAction(timeMs, action);
		public void ScheduleAction(uint timeMs, Action action) => _simulationSystemGroup.ScheduleAction(timeMs, action);

		public void OnEvent(in EventData eventData)
		{
			switch (eventData.eventId) {
				case EventId.HitEventsHit:
					if (!_hittables.ContainsKey(eventData.ItemEntity)) {
						Debug.LogError($"Cannot find entity {eventData.ItemEntity} in hittables.");
					}
					_hittables[eventData.ItemEntity].OnHit(eventData.BallEntity);
					break;

				case EventId.HitEventsUnhit:
					_hittables[eventData.ItemEntity].OnHit(eventData.BallEntity, true);
					break;

				case EventId.LimitEventsBos:
					_rotatables[eventData.ItemEntity].OnRotate(eventData.FloatParam, false);
					break;

				case EventId.LimitEventsEos:
					_rotatables[eventData.ItemEntity].OnRotate(eventData.FloatParam, true);
					break;

				case EventId.SpinnerEventsSpin:
					_spinnables[eventData.ItemEntity].OnSpin();
					break;

				case EventId.FlipperEventsCollide:
					_collidables[eventData.ItemEntity].OnCollide(eventData.BallEntity, eventData.FloatParam);
					break;

				case EventId.SurfaceEventsSlingshot:
					_slingshots[eventData.ItemEntity].OnSlingshot(eventData.BallEntity);
					break;

				default:
					throw new InvalidOperationException($"Unknown event {eventData.eventId} for entity {eventData.ItemEntity}");
			}
		}

		#endregion

		#region API

		public void AddDynamicWire(string switchId, string coilId)
		{
			var switchMapping = _tableComponent.MappingConfig.Switches.FirstOrDefault(c => c.Id == switchId);
			var coilMapping = _tableComponent.MappingConfig.Coils.FirstOrDefault(c => c.Id == coilId);
			if (switchMapping == null) {
				Logger.Warn($"Cannot add new hardware rule for unknown switch \"{switchId}\".");
				return;
			}
			if (coilMapping == null) {
				Logger.Warn($"Cannot add new hardware rule for unknown coil \"{coilId}\".");
				return;
			}

			var wireMapping = new WireMapping($"Hardware rule: {switchId} -> {coilId}", switchMapping, coilMapping);
			_wirePlayer.AddWire(wireMapping);

			// this is for showing it in the editor during runtime only
			_tableComponent.MappingConfig.AddWire(wireMapping);
		}

		public void RemoveDynamicWire(string switchId, string coilId)
		{
			var switchMapping = _tableComponent.MappingConfig.Switches.FirstOrDefault(c => c.Id == switchId);
			var coilMapping = _tableComponent.MappingConfig.Coils.FirstOrDefault(c => c.Id == coilId);
			if (switchMapping == null) {
				Logger.Warn($"Cannot remove hardware rule for unknown switch \"{switchId}\".");
				return;
			}
			if (coilMapping == null) {
				Logger.Warn($"Cannot remove hardware rule for unknown coil \"{coilId}\".");
				return;
			}

			var wireMapping = new WireMapping($"Hardware rule: {switchId} -> {coilId}", switchMapping, coilMapping);
			_wirePlayer.RemoveWire(wireMapping);

			// this is for the editor during runtime only
			var wire = _tableComponent.MappingConfig.Wires.FirstOrDefault(w =>
				w.Description == wireMapping.Description &&
				w.SourceDevice == wireMapping.SourceDevice &&
				w.SourceDeviceItem == wireMapping.SourceDeviceItem &&
				w.SourceInputAction == wireMapping.SourceInputAction &&
				w.SourceInputActionMap == wireMapping.SourceInputActionMap &&
				w.DestinationDevice == wireMapping.DestinationDevice &&
				w.DestinationDeviceItem == wireMapping.DestinationDeviceItem
			);
			_tableComponent.MappingConfig.RemoveWire(wire);
		}

		#endregion

		private static void HandleInput(object obj, InputActionChange change)
		{
			if (obj is InputAction action && action.actionMap.name == InputConstants.MapDebug) {
				var value = action.ReadValue<float>();
				switch (action.name) {
					case InputConstants.ActionSlowMotion: {
						switch (change) {
							case InputActionChange.ActionPerformed when value > 0.1:
								Time.timeScale = math.lerp(1f, SlowMotionMax, value);
								break;
							case InputActionChange.ActionPerformed:
								Time.timeScale = 1;
								break;
							case InputActionChange.ActionStarted:
								Time.timeScale = SlowMotionMax;
								break;
							case InputActionChange.ActionCanceled:
								Time.timeScale = 1;
								break;
						}
						Logger.Info("Timescale = " + Time.timeScale);
						break;
					}
					case InputConstants.ActionTimeLapse: {
						if (change == InputActionChange.ActionPerformed) {
							if (value > 0.1) {
								Time.timeScale = math.lerp(1f, TimeLapseMax, value);
							} else {
								Time.timeScale = 1;
							}
						}
						Logger.Info("Timescale = " + Time.timeScale);
						break;
					}
				}
			}
		}
	}
}
