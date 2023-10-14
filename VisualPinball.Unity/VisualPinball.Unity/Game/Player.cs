// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Game.Engines;
using Color = VisualPinball.Engine.Math.Color;
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

		public event EventHandler OnPlayeStarted;

		public List<SwitchMapping> SwitchMapping => _tableComponent.MappingConfig.Switches;
		public List<CoilMapping> CoilMapping => _tableComponent.MappingConfig.Coils;
		public List<LampMapping> LampMapping => _tableComponent.MappingConfig.Lamps;

		public event EventHandler OnUpdate;

		public event EventHandler<BallEvent> OnBallCreated;
		public event EventHandler<BallEvent> OnBallDestroyed;

		[HideInInspector] [SerializeField] public string debugUiId;

		[Tooltip("When enabled, update the switch, coil, lamp and wire manager windows in the editor (slower performance)")]
		public bool UpdateDuringGamplay = true;

		// table related
		private readonly List<IApi> _apis = new();
		private readonly List<IApiColliderGenerator> _colliderGenerators = new();
		private readonly Dictionary<int, IApiHittable> _hittables = new();
		private readonly Dictionary<int, IApiRotatable> _rotatables = new();
		private readonly Dictionary<int, IApiCollidable> _collidables = new();
		private readonly Dictionary<int, IApiSpinnable> _spinnables = new();
		private readonly Dictionary<int, IApiSlingshot> _slingshots = new();
		private readonly Dictionary<int, IApiDroppable> _droppables = new();

		internal IEnumerable<IApiColliderGenerator> ColliderGenerators => _colliderGenerators;

		// input related
		[NonSerialized] private InputManager _inputManager;
		[NonSerialized] private readonly List<(InputAction, Action<InputAction.CallbackContext>)> _actions = new();

		// players
		[NonSerialized] private readonly LampPlayer _lampPlayer = new();
		[NonSerialized] private readonly CoilPlayer _coilPlayer = new();
		[NonSerialized] private readonly SwitchPlayer _switchPlayer = new();
		[NonSerialized] private readonly WirePlayer _wirePlayer = new();
		[NonSerialized] private readonly DisplayPlayer _displayPlayer = new();

		private const float SlowMotionMax = 0.1f;
		private const float TimeLapseMax = 2.5f;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private TableComponent _tableComponent;
		private PlayfieldComponent _playfieldComponent;
		private PhysicsEngine _physicsEngine;

		private PhysicsEngine PhysicsEngine {
			get {
				if (_physicsEngine == null) {
					_physicsEngine = GetComponent<PhysicsEngine>();
				}
				return _physicsEngine;
			}
		}

		#region Access

		internal IApiSwitch Switch(ISwitchDeviceComponent component, string switchItem) => component != null ? _switchPlayer.Switch(component, switchItem) : null;
		public IApiCoil Coil(ICoilDeviceComponent component, string coilItem) => component != null ? _coilPlayer.Coil(component, coilItem) : null;
		public IApiLamp Lamp(ILampDeviceComponent component) => component != null ? _lampPlayer.Lamp(component) : null;
		public IApiWireDeviceDest WireDevice(IWireableComponent c) => _wirePlayer.WireDevice(c);
		internal void HandleWireSwitchChange(WireDestConfig wireConfig, bool isEnabled) => _wirePlayer.HandleSwitchChange(wireConfig, isEnabled);

		public Dictionary<string, IApiSwitchStatus> SwitchStatuses => _switchPlayer.SwitchStatuses;
		public Dictionary<string, bool> CoilStatuses => _coilPlayer.CoilStatuses;
		public Dictionary<string, LampState> LampStatuses => _lampPlayer.LampStates;
		public Dictionary<string, (bool, float)> WireStatuses => _wirePlayer.WireStatuses;
		public float3 Gravity => _playfieldComponent.Gravity;

		public int NextBallId => ++_currentBallId;
		private int _currentBallId;

		public void SetLamp(string lampId, float value) => _lampPlayer.HandleLampEvent(lampId, value);
		public void SetLamp(string lampId, LampStatus status) => _lampPlayer.HandleLampEvent(lampId, status);
		public void SetLamp(string lampId, Color color) => _lampPlayer.HandleLampEvent(lampId, color);

		#endregion

		#region Lifecycle

		public Player()
		{
			TableApi = new TableApi(this);
		}

		private void Awake()
		{
			DebugLogger.ClearLog();
			_tableComponent = GetComponent<TableComponent>();
			_playfieldComponent = GetComponentInChildren<PlayfieldComponent>();
			var engineComponent = GetComponent<IGamelogicEngine>();

			_apis.Add(TableApi);

			BallManager = new BallManager(GetComponentInChildren<PhysicsEngine>(), this, Playfield.transform);
			_inputManager = new InputManager();
			_inputManager.Enable(HandleInput);

			if (engineComponent != null) {
				GamelogicEngine = engineComponent;
				_lampPlayer.Awake(this, _tableComponent, GamelogicEngine);
				_coilPlayer.Awake(this, _tableComponent, GamelogicEngine, _lampPlayer, _wirePlayer);
				_switchPlayer.Awake(_tableComponent, GamelogicEngine, _inputManager);
				_wirePlayer.Awake(_tableComponent, _inputManager, _switchPlayer, this, PhysicsEngine);
				_displayPlayer.Awake(GamelogicEngine);
			}
		}

		private void Start()
		{
			#if FPS60_IOS && UNITY_IOS && !UNITY_EDITOR
				Application.targetFrameRate = 60;
			#endif

			// trigger init events now
			foreach (var i in _apis) {
				i.OnInit(BallManager);
			}

			_coilPlayer.OnStart();
			_switchPlayer.OnStart();
			_lampPlayer.OnStart();
			_wirePlayer.OnStart();

			GamelogicEngine?.OnInit(this, TableApi, BallManager);
		}

		private void Update()
		{
			OnUpdate?.Invoke(this, EventArgs.Empty);
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

		public void RegisterBumper(BumperComponent component)
		{
			Register(new BumperApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterFlipper(FlipperComponent component)
		{
			Register(new FlipperApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterDropTarget(DropTargetComponent component)
		{
			Register(new DropTargetApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterGate(GateComponent component)
		{
			Register(new GateApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterGateLifter(GateLifterComponent component)
		{
			Register(new GateLifterApi(component.gameObject, this), component);
		}

		public void RegisterHitTarget(HitTargetComponent component)
		{
			Register(new HitTargetApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterKicker(KickerComponent component)
		{
			Register(new KickerApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterLamp(LightComponent component)
		{
			Register(component.GetApi(this), component);
		}

		public void RegisterLampGroup(LightGroupComponent component)
		{
			Register(component.GetApi(this), component);
		}

		public void RegisterStepRotator(StepRotatorMechComponent component)
		{
			Register(new StepRotatorMechApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterScoreMotorComponent(ScoreMotorComponent component)
		{
			Register(new ScoreMotorApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterDropTargetBankComponent(DropTargetBankComponent component)
		{
			Register(new DropTargetBankApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterCollisionSwitchComponent(CollisionSwitchComponent component)
		{
			Register(new CollisionSwitchApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterSlingshotComponent(SlingshotComponent component)
		{
			Register(new SlingshotApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterPlunger(PlungerComponent component, InputActionReference actionRef)
		{
			var plungerApi = new PlungerApi(component.gameObject, this, PhysicsEngine);
			Register(plungerApi, component);

			if (actionRef != null) {
				actionRef.action.performed += plungerApi.OnAnalogPlunge;
				_actions.Add((actionRef.action, plungerApi.OnAnalogPlunge));
			}
		}

		public void RegisterPlayfield(GameObject go)
		{
			PlayfieldApi = new PlayfieldApi(go, this, PhysicsEngine);
			_colliderGenerators.Add(PlayfieldApi);
		}

		public void RegisterPrimitive(PrimitiveComponent component)
		{
			Register(new PrimitiveApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterRamp(RampComponent component)
		{
			Register(new RampApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterRubber(RubberComponent component)
		{
			Register(new RubberApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterSpinner(SpinnerComponent component)
		{
			Register(new SpinnerApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterSurface(SurfaceComponent component)
		{
			Register(new SurfaceApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterTeleporter(TeleporterComponent component)
		{
			Register(new TeleporterApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterTrigger(TriggerComponent component)
		{
			Register(new TriggerApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterTrough(TroughComponent component)
		{
			Register(new TroughApi(component.gameObject, this, PhysicsEngine), component);
		}

		public void RegisterMetalWireGuide(MetalWireGuideComponent component)
		{
			Register(new MetalWireGuideApi(component.gameObject, this, PhysicsEngine), component);
		}

		private void Register<TApi>(TApi api, MonoBehaviour component) where TApi : IApi
		{
			TableApi.Register(component, api);
			_apis.Add(api);
			var itemId = component.gameObject.GetInstanceID();
			Debug.Log($"Registering {component.GetType()} {component.name} with ID {itemId}.");
			if (api is IApiRotatable rotatable) {
				_rotatables[itemId] = rotatable;
			}
			if (api is IApiSlingshot slingshot) {
				_slingshots[itemId] = slingshot;
			}
			if (api is IApiDroppable droppable) {
				_droppables[itemId] = droppable;
			}
			if (api is IApiSpinnable spinnable) {
				_spinnables[itemId] = spinnable;
			}
			if (api is IApiSwitchDevice switchDevice) {
				if (component is ISwitchDeviceComponent switchDeviceComponent) {
					_switchPlayer.RegisterSwitchDevice(switchDeviceComponent, switchDevice);
				} else  {
					Logger.Warn($"{component.GetType()} is not of type ISwitchDeviceComponent while ${api.GetType()} is of type IApiSwitchDevice.");
				}
			}
			if (api is IApiCoilDevice coilDevice) {
				if (component is ICoilDeviceComponent coilDeviceComponent) {
					_coilPlayer.RegisterCoilDevice(coilDeviceComponent, coilDevice);
				} else {
					Logger.Warn($"{component.GetType()} is not of type ICoilDeviceComponent while ${api.GetType()} is of type IApiCoilDevice.");
				}
			}
			if (api is IApiWireDeviceDest wireDevice) {
				if (component is IWireableComponent wireableComponent) {
					_wirePlayer.RegisterWireDevice(wireableComponent, wireDevice);
				} else {
					Logger.Warn($"{component.GetType()} is not of type IWireableComponent while ${api.GetType()} is of type IApiWireDeviceDest.");
				}
			}

			if (api is IApiLamp lamp) {
				if (component is ILampDeviceComponent lampDeviceComponent) {
					_lampPlayer.RegisterLamp(lampDeviceComponent, lamp);
				} else {
					Logger.Warn($"{component.GetType()} is not of type ILampDeviceComponent while ${api.GetType()} is of type IApiLamp.");
				}
			}

			if (api is IApiColliderGenerator colliderGenerator) {
				RegisterCollider(itemId, colliderGenerator);
			}
		}

		private void RegisterCollider(int itemId, IApiColliderGenerator apiColl)
		{
			if (!apiColl.IsColliderAvailable) {
				return;
			}
			_colliderGenerators.Add(apiColl);
			if (apiColl is IApiHittable apiHittable) {
				_hittables[itemId] = apiHittable;
			}

			if (apiColl is IApiCollidable apiCollidable) {
				_collidables[itemId] = apiCollidable;
			}
		}

		#endregion

		#region Events

		public void ScheduleAction(int timeMs, Action action) => PhysicsEngine.ScheduleAction(timeMs, action);
		public void ScheduleAction(uint timeMs, Action action) => PhysicsEngine.ScheduleAction(timeMs, action);

		public void OnEvent(in EventData eventData)
		{
			Debug.Log(eventData);
			switch (eventData.EventId) {
				case EventId.HitEventsHit:
					if (!_hittables.ContainsKey(eventData.ItemId)) {
						Debug.LogError($"Cannot find {eventData.ItemId} in hittables.");
					}
					_hittables[eventData.ItemId].OnHit(eventData.BallId);
					break;

				case EventId.HitEventsUnhit:
					_hittables[eventData.ItemId].OnHit(eventData.BallId, true);
					break;

				case EventId.LimitEventsBos:
					_rotatables[eventData.ItemId].OnRotate(eventData.FloatParam, false);
					break;

				case EventId.LimitEventsEos:
					_rotatables[eventData.ItemId].OnRotate(eventData.FloatParam, true);
					break;

				case EventId.SpinnerEventsSpin:
					_spinnables[eventData.ItemId].OnSpin();
					break;

				case EventId.FlipperEventsCollide:
					_collidables[eventData.ItemId].OnCollide(eventData.BallId, eventData.FloatParam);
					break;

				case EventId.SurfaceEventsSlingshot:
					_slingshots[eventData.ItemId].OnSlingshot(eventData.BallId);
					break;

				case EventId.TargetEventsDropped:
					_droppables[eventData.ItemId].OnDropStatusChanged(true, eventData.BallId);
					break;

				case EventId.TargetEventsRaised:
					_droppables[eventData.ItemId].OnDropStatusChanged(false, eventData.BallId);
					break;

				default:
					throw new InvalidOperationException($"Unknown event {eventData.EventId} for entity {eventData.ItemId}");
			}
		}

		internal void BallCreated(int ballId, GameObject ball) => OnBallCreated?.Invoke(this, new BallEvent(ballId, ball));

		internal void BallDestroyed(int ballId, GameObject ball) => OnBallDestroyed?.Invoke(this, new BallEvent(ballId, ball));

		#endregion

		#region API

		public void AddHardwareRule(string switchId, string coilId)
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

			var wireMapping = new WireMapping($"Hardware rule: {switchId} -> {coilId}", switchMapping, coilMapping).WithId();
			_wirePlayer.AddWire(wireMapping);

			// this is for showing it in the editor during runtime only
			_tableComponent.MappingConfig.AddWire(wireMapping);
		}

		public void RemoveHardwareRule(string switchId, string coilId)
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

			// todo this can be done more elegantly with Ids now.
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

	public readonly struct BallEvent
	{
		public readonly int BallId;
		public readonly GameObject Ball;

		public BallEvent(int ballId, GameObject ball)
		{
			BallId = ballId;
			Ball = ball;
		}
	}
}
