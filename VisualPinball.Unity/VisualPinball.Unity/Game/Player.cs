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
using VisualPinball.Engine.VPT.Mappings;
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

		internal IApiSwitch Switch(string n) => _switchPlayer.Switch(n);
		internal IApiWireDest Wire(string n) => _wirePlayer.Wire(n);
		internal IApiWireDeviceDest WireDevice(string n) => _wirePlayer.WireDevice(n);
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
				_lampPlayer.Awake(_tableContainer, GamelogicEngine);
				_coilPlayer.Awake(_tableContainer, GamelogicEngine, _lampPlayer);
				_switchPlayer.Awake(_tableContainer, GamelogicEngine, _inputManager);
				_wirePlayer.Awake(_tableContainer, _inputManager, _switchPlayer);
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
			Register(TableApi.Bumpers, new BumperApi(component.gameObject, entity, parentEntity, this), entity);
			RegisterTransform<BumperRingAnimationAuthoring>(BumperRingTransforms, component, entity);
			RegisterTransform<BumperSkirtAnimationAuthoring>(BumperSkirtTransforms, component, entity);
		}

		public void RegisterFlipper(FlipperAuthoring component, Entity entity, Entity parentEntity)
		{
			var go = component.gameObject;
			var flipperApi = new FlipperApi(go, entity, parentEntity, this);
			TableApi.Flippers[go.name] = flipperApi;
			_apis.Add(flipperApi);
			_initializables.Add(flipperApi);
			_rotatables[entity] = flipperApi;
			_switchPlayer.RegisterSwitch(go.name, flipperApi);
			_coilPlayer.RegisterCoil(go.name, flipperApi);
			_wirePlayer.RegisterWire(go.name, flipperApi);

			RegisterCollider(entity, flipperApi);

			if (EngineProvider<IDebugUI>.Exists) {
				EngineProvider<IDebugUI>.Get().OnRegisterFlipper(entity, go.name);
			}

			FlipperTransforms[entity] = go.transform;
		}

		public void RegisterGate(GateAuthoring component, Entity entity, Entity parentEntity)
		{
			var go = component.gameObject;
			var gateApi = new GateApi(go, entity, parentEntity, this);
			TableApi.Gates[go.name] = gateApi;
			_apis.Add(gateApi);
			_initializables.Add(gateApi);
			_rotatables[entity] = gateApi;
			_switchPlayer.RegisterSwitch(go.name, gateApi);

			RegisterCollider(entity, gateApi);

			var wireAnimAuthoring = go.GetComponentInChildren<GateWireAnimationAuthoring>();
			if (wireAnimAuthoring) {
				GateWireTransforms[entity] = wireAnimAuthoring.gameObject.transform;
			}
		}

		public void RegisterHitTarget(HitTargetAuthoring component, Entity entity, Entity parentEntity)
		{
			var go = component.gameObject;
			var hitTargetApi = new HitTargetApi(go, entity, parentEntity, this);
			TableApi.HitTargets[go.name] = hitTargetApi;
			_apis.Add(hitTargetApi);
			_initializables.Add(hitTargetApi);
			RegisterCollider(entity, hitTargetApi);

			var hitTargetMovementAuth = go.GetComponentInChildren<HitTargetAnimationAuthoring>();
			if (hitTargetMovementAuth) {
				HitTargetTransforms[entity] = hitTargetMovementAuth.gameObject.transform;
			}

			var dropMovementAuth = go.GetComponentInChildren<DropTargetAnimationAuthoring>();
			if (dropMovementAuth) {
				DropTargetTransforms[entity] = dropMovementAuth.gameObject.transform;
			}
			_switchPlayer.RegisterSwitch(go.name, hitTargetApi);
		}

		public void RegisterKicker(KickerAuthoring component, Entity entity, Entity parentEntity)
		{
			var go = component.gameObject;
			var kickerApi = new KickerApi(go, entity, parentEntity, this);
			TableApi.Kickers[go.name] = kickerApi;
			_apis.Add(kickerApi);
			_initializables.Add(kickerApi);
			_switchPlayer.RegisterSwitch(go.name, kickerApi);
			_coilPlayer.RegisterCoil(go.name, kickerApi);
			_wirePlayer.RegisterWire(go.name, kickerApi);

			RegisterCollider(entity, kickerApi);
		}

		public void RegisterLamp(LightAuthoring component)
		{
			var go = component.gameObject;
			var lightApi = new LightApi(go, this);
			TableApi.Lights[go.name] = lightApi;
			_apis.Add(lightApi);
			_initializables.Add(lightApi);
			_lampPlayer.RegisterLamp(go.name, lightApi);
			_wirePlayer.RegisterWire(go.name, lightApi);
		}

		public void RegisterPlunger(PlungerAuthoring component, Entity entity, Entity parentEntity, InputActionReference actionRef)
		{
			var go = component.gameObject;
			var plungerApi = new PlungerApi(go, entity, parentEntity, this);
			TableApi.Plungers[go.name] = plungerApi;
			_apis.Add(plungerApi);
			_colliderGenerators.Add(plungerApi);
			_initializables.Add(plungerApi);
			_rotatables[entity] = plungerApi;
			_coilPlayer.RegisterCoilDevice(go.name, plungerApi);
			_wirePlayer.RegisterWireDevice(go.name, plungerApi);

			if (actionRef != null) {
				actionRef.action.performed += plungerApi.OnAnalogPlunge;
				_actions.Add((actionRef.action, plungerApi.OnAnalogPlunge));
			}

			PlungerSkinnedMeshRenderers[entity] = go.GetComponentsInChildren<SkinnedMeshRenderer>();
		}

		public void RegisterPlayfield(GameObject go)
		{
			PlayfieldApi = new PlayfieldApi(go, this);
			_colliderGenerators.Add(PlayfieldApi);
		}

		public void RegisterPrimitive(PrimitiveAuthoring component, Entity entity, Entity parentEntity)
		{
			var go = component.gameObject;
			var primitiveApi = new PrimitiveApi(go, entity, parentEntity, this);
			TableApi.Primitives[go.name] = primitiveApi;
			_apis.Add(primitiveApi);
			_initializables.Add(primitiveApi);
			RegisterCollider(entity, primitiveApi);
		}

		public void RegisterRamp(RampAuthoring component, Entity entity, Entity parentEntity)
		{
			var go = component.gameObject;
			var rampApi = new RampApi(go, entity, parentEntity, this);
			TableApi.Ramps[go.name] = rampApi;
			_apis.Add(rampApi);
			_initializables.Add(rampApi);
			RegisterCollider(entity, rampApi);
		}

		public void RegisterRubber(RubberAuthoring component, Entity entity, Entity parentEntity)
		{
			var go = component.gameObject;
			var rubberApi = new RubberApi(go, entity, parentEntity, this);
			TableApi.Rubbers[go.name] = rubberApi;
			_apis.Add(rubberApi);
			_initializables.Add(rubberApi);
			RegisterCollider(entity, rubberApi);
		}

		public void RegisterSpinner(SpinnerAuthoring component, Entity entity, Entity parentEntity)
		{
			var go = component.gameObject;
			var spinnerApi = new SpinnerApi(go, entity, parentEntity, this);
			TableApi.Spinners[go.name] = spinnerApi;
			_apis.Add(spinnerApi);
			_initializables.Add(spinnerApi);
			_spinnables[entity] = spinnerApi;
			_rotatables[entity] = spinnerApi;
			_switchPlayer.RegisterSwitch(go.name, spinnerApi);

			RegisterCollider(entity, spinnerApi);

			var plateAnimAuthoring = go.GetComponentInChildren<SpinnerPlateAnimationAuthoring>();
			if (plateAnimAuthoring) {
				SpinnerPlateTransforms[entity] = plateAnimAuthoring.gameObject.transform;
			}
		}

		public void RegisterSurface(SurfaceAuthoring component, Entity entity, Entity parentEntity)
		{
			var go = component.gameObject;
			var surfaceApi = new SurfaceApi(go, entity, parentEntity, this);
			TableApi.Surfaces[go.name] = surfaceApi;
			_apis.Add(surfaceApi);
			_initializables.Add(surfaceApi);
			_slingshots[entity] = surfaceApi;

			RegisterCollider(entity, surfaceApi);
		}

		public void RegisterTrigger(TriggerAuthoring component, Entity entity, Entity parentEntity)
		{
			var go = component.gameObject;
			var triggerApi = new TriggerApi(go, entity, parentEntity, this);
			TableApi.Triggers[go.name] = triggerApi;
			_apis.Add(triggerApi);
			_initializables.Add(triggerApi);
			_switchPlayer.RegisterSwitch(go.name, triggerApi);

			RegisterCollider(entity, triggerApi);

			TriggerTransforms[entity] = go.transform;
		}

		public void RegisterTrigger(TriggerData data, Entity entity, GameObject go, bool addComponent = false)
		{
			var triggerApi = new TriggerApi(go, entity, Entity.Null, this);
			if (addComponent) {
				go.AddComponent<TriggerAuthoring>().SetData(data);
			}
			TableApi.Triggers[go.name] = triggerApi;
			_apis.Add(triggerApi);
			_initializables.Add(triggerApi);
			_colliderGenerators.Add(triggerApi);
			_hittables[entity] = triggerApi;
			_switchPlayer.RegisterSwitch(go.name, triggerApi);
		}

		public void RegisterTrough(TroughAuthoring component)
		{
			var go = component.gameObject;
			var troughApi = new TroughApi(go, this);
			TableApi.Troughs[go.name] = troughApi;
			_apis.Add(troughApi);
			_initializables.Add(troughApi);
			_switchPlayer.RegisterSwitchDevice(go.name, troughApi);
			_coilPlayer.RegisterCoilDevice(go.name, troughApi);
		}

		private void Register<TApi>(Dictionary<string, TApi> apis, TApi api, Entity entity) where TApi : IApi
		{
			apis[api.Name] = api;
			_apis.Add(api);
			if (api is IApiInitializable initializable) {
				_initializables.Add(initializable);
			}
			if (api is IApiSwitchDevice switchDevice) {
				_switchPlayer.RegisterSwitchDevice(api.Name, switchDevice);
			}
			if (api is IApiCoilDevice coilDevice) {
				_coilPlayer.RegisterCoilDevice(api.Name, coilDevice);
			}
			if (api is IApiWireDeviceDest wireDevice) {
				_wirePlayer.RegisterWireDevice(api.Name, wireDevice);
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
			var switchMapping = _tableContainer.Mappings.Data.Switches.FirstOrDefault(c => c.Id == switchId);
			var coilMapping = _tableContainer.Mappings.Data.Coils.FirstOrDefault(c => c.Id == coilId);
			if (switchMapping == null) {
				Logger.Warn($"Cannot add new hardware rule for unknown switch \"{switchId}\".");
				return;
			}
			if (coilMapping == null) {
				Logger.Warn($"Cannot add new hardware rule for unknown coil \"{coilId}\".");
				return;
			}

			var wireMapping = new MappingsWireData($"Hardware rule: {switchId} -> {coilId}", switchMapping, coilMapping);
			_wirePlayer.AddWire(wireMapping);

			// this is for showing it in the editor during runtime only
			_tableContainer.Mappings.Data.AddWire(wireMapping);
		}

		public void RemoveDynamicWire(string switchId, string coilId)
		{
			var switchMapping = _tableContainer.Mappings.Data.Switches.FirstOrDefault(c => c.Id == switchId);
			var coilMapping = _tableContainer.Mappings.Data.Coils.FirstOrDefault(c => c.Id == coilId);
			if (switchMapping == null) {
				Logger.Warn($"Cannot remove hardware rule for unknown switch \"{switchId}\".");
				return;
			}
			if (coilMapping == null) {
				Logger.Warn($"Cannot remove hardware rule for unknown coil \"{coilId}\".");
				return;
			}

			var wireMapping = new MappingsWireData($"Hardware rule: {switchId} -> {coilId}", switchMapping, coilMapping);
			_wirePlayer.RemoveWire(wireMapping);

			// this is for the editor during runtime only
			var wire = _tableContainer.Mappings.Data.Wires.FirstOrDefault(w =>
				w.Description == wireMapping.Description &&
				w.SourceDevice == wireMapping.SourceDevice &&
				w.SourceDeviceItem == wireMapping.SourceDeviceItem &&
				w.SourceInputAction == wireMapping.SourceInputAction &&
				w.SourceInputActionMap == wireMapping.SourceInputActionMap &&
				w.SourcePlayfieldItem == wireMapping.SourcePlayfieldItem &&
				w.Destination == wireMapping.Destination &&
				w.DestinationDevice == wireMapping.DestinationDevice &&
				w.DestinationDeviceItem == wireMapping.DestinationDeviceItem &&
				w.DestinationPlayfieldItem == wireMapping.DestinationPlayfieldItem
			);
			_tableContainer.Mappings.Data.RemoveWire(wire);
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
