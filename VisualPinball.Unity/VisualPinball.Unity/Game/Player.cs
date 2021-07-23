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
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Mappings;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Engine.VPT.Trough;
using Light = VisualPinball.Engine.VPT.Light.Light;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class Player : MonoBehaviour
	{
		public Table Table { get; private set; }
		public TableApi TableApi { get; private set; }

		// shortcuts
		public GameObject Playfield => GetComponentInChildren<TablePlayfieldAuthoring>().gameObject;

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

		internal readonly Dictionary<Entity, Flipper> Flippers = new Dictionary<Entity, Flipper>();
		internal readonly Dictionary<Entity, Transform> FlipperTransforms = new Dictionary<Entity, Transform>();
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

		internal static readonly Entity TableEntity = new Entity {Index = -3, Version = 0}; // a fake entity we just use for reference

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		#region Access

		internal IApiSwitch Switch(string n) => _switchPlayer.Switch(n);
		internal IApiWireDest Wire(string n) => _wirePlayer.Wire(n);
		internal IApiWireDeviceDest WireDevice(string n) => _wirePlayer.WireDevice(n);
		public Dictionary<string, bool> SwitchStatusesClosed => _switchPlayer.SwitchStatusesClosed;
		public Dictionary<string, bool> CoilStatuses => _coilPlayer.CoilStatuses;
		public Dictionary<string, float> LampStatuses => _lampPlayer.LampStatuses;

		#endregion

		#region Lifecycle

		public Player()
		{
			TableApi = new TableApi(this);
		}

		private void Awake()
		{
			var tableComponent = gameObject.GetComponent<TableAuthoring>();
			var engineComponent = GetComponent<IGamelogicEngine>();

			tableComponent.TableContainer.Refresh();

			TableApi.Data = tableComponent.Data;
			_initializables.Add(TableApi);
			_colliderGenerators.Add(TableApi);

			Table = tableComponent.Table; //tableComponent.CreateTable(tableComponent.Data);
			_tableContainer = tableComponent.TableContainer;
			BallManager = new BallManager(Table, this);
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
			EngineProvider<IPhysicsEngine>.Get().Init(tableComponent, BallManager);
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

		public void RegisterBumper(Bumper bumper, Entity entity, Entity parentEntity, GameObject go)
		{
			var colliderAuth = go.GetComponent<BumperColliderAuthoring>();
			var bumperApi = new BumperApi(bumper, entity, parentEntity, this);
			TableApi.Bumpers[bumper.Name] = bumperApi;
			_apis.Add(bumperApi);
			_initializables.Add(bumperApi);
			if (colliderAuth) {
				_colliderGenerators.Add(bumperApi);
				_hittables[entity] = bumperApi;
			}
			_switchPlayer.RegisterSwitch(bumper, bumperApi);
			_coilPlayer.RegisterCoil(bumper, bumperApi);
			_wirePlayer.RegisterWire(bumper, bumperApi);
		}

		public void RegisterFlipper(Flipper flipper, Entity entity, Entity parentEntity, GameObject go)
		{
			var colliderAuth = go.GetComponent<FlipperColliderAuthoring>();
			var flipperApi = new FlipperApi(flipper, entity, parentEntity, this);
			TableApi.Flippers[flipper.Name] = flipperApi;
			_apis.Add(flipperApi);
			_initializables.Add(flipperApi);
			if (colliderAuth) {
				_colliderGenerators.Add(flipperApi);
				_collidables[entity] = flipperApi;
				_hittables[entity] = flipperApi;
			}
			Flippers[entity] = flipper;
			_rotatables[entity] = flipperApi;
			_switchPlayer.RegisterSwitch(flipper, flipperApi);
			_coilPlayer.RegisterCoil(flipper, flipperApi);
			_wirePlayer.RegisterWire(flipper, flipperApi);
			FlipperTransforms[entity] = go.transform;

			if (EngineProvider<IDebugUI>.Exists) {
				EngineProvider<IDebugUI>.Get().OnRegisterFlipper(entity, flipper.Name);
			}
		}

		public void RegisterGate(Gate gate, Entity entity, Entity parentEntity, GameObject go)
		{
			var colliderAuth = go.GetComponent<GateColliderAuthoring>();
			var gateApi = new GateApi(gate, entity, parentEntity, this);
			TableApi.Gates[gate.Name] = gateApi;
			_apis.Add(gateApi);
			_initializables.Add(gateApi);
			if (colliderAuth) {
				_colliderGenerators.Add(gateApi);
				_hittables[entity] = gateApi;
			}
			_rotatables[entity] = gateApi;
			_switchPlayer.RegisterSwitch(gate, gateApi);
		}

		public void RegisterHitTarget(HitTarget hitTarget, Entity entity, Entity parentEntity, GameObject go)
		{
			var colliderAuth = go.GetComponent<HitTargetColliderAuthoring>();
			var hitTargetApi = new HitTargetApi(hitTarget, entity, parentEntity, colliderAuth == null ? null : colliderAuth.PhysicsMaterial, this);
			TableApi.HitTargets[hitTarget.Name] = hitTargetApi;
			_apis.Add(hitTargetApi);
			_initializables.Add(hitTargetApi);
			if (colliderAuth) {
				_colliderGenerators.Add(hitTargetApi);
				_hittables[entity] = hitTargetApi;
			}
			_switchPlayer.RegisterSwitch(hitTarget, hitTargetApi);
		}

		public void RegisterKicker(Kicker kicker, Entity entity, Entity parentEntity, GameObject go)
		{
			var colliderAuth = go.GetComponent<KickerColliderAuthoring>();
			var kickerApi = new KickerApi(kicker, entity, parentEntity, this);
			TableApi.Kickers[kicker.Name] = kickerApi;
			_apis.Add(kickerApi);
			_initializables.Add(kickerApi);
			if (colliderAuth) {
				_colliderGenerators.Add(kickerApi);
				_hittables[entity] = kickerApi;
			}
			_switchPlayer.RegisterSwitch(kicker, kickerApi);
			_coilPlayer.RegisterCoil(kicker, kickerApi);
			_wirePlayer.RegisterWire(kicker, kickerApi);
		}

		public void RegisterLamp(Light lamp, GameObject go)
		{
			var lightApi = new LightApi(lamp, go, this);
			TableApi.Lights[lamp.Name] = lightApi;
			_apis.Add(lightApi);
			_initializables.Add(lightApi);
			_lampPlayer.RegisterLamp(lamp, lightApi);
			_wirePlayer.RegisterWire(lamp, lightApi);
		}

		public void RegisterPlunger(Plunger plunger, Entity entity, Entity parentEntity, InputActionReference actionRef)
		{
			var plungerApi = new PlungerApi(plunger, entity, parentEntity, this);
			TableApi.Plungers[plunger.Name] = plungerApi;
			_apis.Add(plungerApi);
			_colliderGenerators.Add(plungerApi);
			_initializables.Add(plungerApi);
			_rotatables[entity] = plungerApi;
			_coilPlayer.RegisterCoilDevice(plunger, plungerApi);
			_wirePlayer.RegisterWireDevice(plunger, plungerApi);

			if (actionRef != null) {
				actionRef.action.performed += plungerApi.OnAnalogPlunge;
				_actions.Add((actionRef.action, plungerApi.OnAnalogPlunge));
			}
		}

		public void RegisterPrimitive(Primitive primitive, Entity entity, Entity parentEntity, GameObject go)
		{
			var colliderAuth = go.GetComponent<PrimitiveColliderAuthoring>();
			var primitiveApi = new PrimitiveApi(primitive, entity, parentEntity, colliderAuth == null ? null : colliderAuth.PhysicsMaterial, this);
			TableApi.Primitives[primitive.Name] = primitiveApi;
			_apis.Add(primitiveApi);
			if (colliderAuth) {
				_colliderGenerators.Add(primitiveApi);
				_hittables[entity] = primitiveApi;
			}
			_initializables.Add(primitiveApi);
		}

		public void RegisterRamp(Ramp ramp, Entity entity, Entity parentEntity, GameObject go)
		{
			var colliderAuth = go.GetComponent<RampColliderAuthoring>();
			var rampApi = new RampApi(ramp, entity, parentEntity, colliderAuth == null ? null : colliderAuth.PhysicsMaterial, this);
			TableApi.Ramps[ramp.Name] = rampApi;
			_apis.Add(rampApi);
			_initializables.Add(rampApi);
			if (colliderAuth) {
				_colliderGenerators.Add(rampApi);
			}
		}

		public void RegisterRubber(Rubber rubber, Entity entity, Entity parentEntity, GameObject go)
		{
			var colliderAuth = go.GetComponent<RubberColliderAuthoring>();
			var rubberApi = new RubberApi(rubber, entity, parentEntity, colliderAuth == null ? null : colliderAuth.PhysicsMaterial, this);
			TableApi.Rubbers[rubber.Name] = rubberApi;
			_apis.Add(rubberApi);
			_initializables.Add(rubberApi);
			if (colliderAuth) {
				_colliderGenerators.Add(rubberApi);
				_hittables[entity] = rubberApi;
			}
		}

		public void RegisterSurface(Surface surface, Entity entity, Entity parentEntity, GameObject go)
		{
			var colliderAuth = go.GetComponent<SurfaceColliderAuthoring>();
			var surfaceApi = new SurfaceApi(surface, entity, parentEntity, colliderAuth == null ? null : colliderAuth.PhysicsMaterial, this);
			TableApi.Surfaces[surface.Name] = surfaceApi;
			_apis.Add(surfaceApi);
			_initializables.Add(surfaceApi);
			if (colliderAuth) {
				_colliderGenerators.Add(surfaceApi);
				_hittables[entity] = surfaceApi;
			}
			_slingshots[entity] = surfaceApi;
		}

		public void RegisterSpinner(Spinner spinner, Entity entity, Entity parentEntity, GameObject go)
		{
			var colliderAuth = go.GetComponent<SpinnerColliderAuthoring>();
			var spinnerApi = new SpinnerApi(spinner, entity, parentEntity, this);
			TableApi.Spinners[spinner.Name] = spinnerApi;
			_apis.Add(spinnerApi);
			_initializables.Add(spinnerApi);
			if (colliderAuth) {
				_colliderGenerators.Add(spinnerApi);
			}
			_spinnables[entity] = spinnerApi;
			_rotatables[entity] = spinnerApi;
			_switchPlayer.RegisterSwitch(spinner, spinnerApi);
		}

		public void RegisterTrigger(Trigger trigger, Entity entity, Entity parentEntity, GameObject go)
		{
			var colliderAuth = go.GetComponent<TriggerColliderAuthoring>();
			var triggerApi = new TriggerApi(trigger, entity, parentEntity, this);
			TableApi.Triggers[trigger.Name] = triggerApi;
			_apis.Add(triggerApi);
			_initializables.Add(triggerApi);
			if (colliderAuth) {
				_colliderGenerators.Add(triggerApi);
				_hittables[entity] = triggerApi;
			}
			_switchPlayer.RegisterSwitch(trigger, triggerApi);
		}

		public void RegisterTrigger(Trigger trigger, Entity entity)
		{
			var triggerApi = new TriggerApi(trigger, entity, Entity.Null, this);
			TableApi.Triggers[trigger.Name] = triggerApi;
			_apis.Add(triggerApi);
			_initializables.Add(triggerApi);
			_colliderGenerators.Add(triggerApi);
			_hittables[entity] = triggerApi;
			_switchPlayer.RegisterSwitch(trigger, triggerApi);
		}

		public void RegisterTrough(Trough trough, GameObject go)
		{
			var troughApi = new TroughApi(trough, this);
			TableApi.Troughs[trough.Name] = troughApi;
			_apis.Add(troughApi);
			_initializables.Add(troughApi);
			_switchPlayer.RegisterSwitchDevice(trough, troughApi);
			_coilPlayer.RegisterCoilDevice(trough, troughApi);
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

		public float3 GetGravity()
		{
			var slope = Table.Data.AngleTiltMin + (Table.Data.AngleTiltMax - Table.Data.AngleTiltMin) * Table.Data.GlobalDifficulty;
			var strength = Table.Data.OverridePhysics != 0 ? PhysicsConstants.DefaultTableGravity : Table.Data.Gravity;
			return new float3(0, math.sin(math.radians(slope)) * strength, -math.cos(math.radians(slope)) * strength);
		}
	}
}
