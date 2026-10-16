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
using System.Threading;
using NLog;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Plugins;
using VisualPinball.Unity.Simulation;
using Color = VisualPinball.Engine.Math.Color;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[PackAs("Player")]
	public class Player : MonoBehaviour, IPackable
	{
		#region Data

		[Tooltip("When enabled, update the switch, coil, lamp and wire manager windows in the editor (slower performance)")]
		public bool UpdateDuringGameplay = true;

		#endregion

		#region Packaging

		public byte[] Pack() => PlayerPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => null;

		public void Unpack(byte[] bytes) => PlayerPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) { }

		#endregion

		private TableApi _tableApi;
		public TableApi TableApi => _tableApi ??= new(this);
		public PlayfieldApi PlayfieldApi { get; private set; }

		// shortcuts
		public GameObject Playfield => PlayfieldComponent.gameObject;

		[NonSerialized]
		public IGamelogicEngine GamelogicEngine;

		[NonSerialized]
		public BallManager BallManager;

		public event EventHandler OnPlayerStarted;

		public List<SwitchMapping> SwitchMapping => _tableComponent.MappingConfig.Switches;
		public List<CoilMapping> CoilMapping => _tableComponent.MappingConfig.Coils;
		public List<LampMapping> LampMapping => _tableComponent.MappingConfig.Lamps;

		public event EventHandler OnUpdate;
		internal event Action<string, bool> CabinetInputActionChanged;

		public event EventHandler<BallEvent> OnBallCreated;
		public event EventHandler<BallEvent> OnBallDestroyed;

		public float4x4 PlayfieldToWorldMatrix => PlayfieldComponent.transform.localToWorldMatrix;

		[HideInInspector] [SerializeField] public string debugUiId;

		// table related
		private readonly List<IApi> _apis = new();
		private readonly List<IApiColliderGenerator> _colliderGenerators = new();
		private readonly Dictionary<int, IApiHittable> _hittables = new();
		private readonly Dictionary<int, IApiRotatable> _rotatables = new();
		private readonly Dictionary<int, IApiCollidable> _collidables = new();
		private readonly Dictionary<int, IApiSpinnable> _spinnables = new();
		private readonly Dictionary<int, IApiSlingshot> _slingshots = new();
		private readonly Dictionary<int, IApiDroppable> _droppables = new();
		private readonly Dictionary<int, IApiMagnetEvents> _magnets = new();

		internal IEnumerable<IApiColliderGenerator> ColliderGenerators => _colliderGenerators;

		// input related
		[NonSerialized] private InputManager _inputManager;
		[NonSerialized] private readonly List<(InputAction, Action<InputAction.CallbackContext>)> _actions = new();
		[NonSerialized] private readonly System.Random _nudgeRandom = new System.Random();

		// Default-keyboard-nudge suppression (Unity input path only): the nudge counter is
		// snapshotted before each input-system update, i.e. before any action callback (and
		// therefore any synchronous gamelogic reaction) of that update ran. Nudge key presses
		// are recorded with that snapshot and evaluated in Update(), after all callbacks ran:
		// if gamelogic called Nudge() in reaction to the key, the counter moved and the
		// default nudge is suppressed — mirroring the tight bracket of the reference and of
		// the simulation-thread path.
		[NonSerialized] private int _nudgeIndexAtInputUpdateStart;
		[NonSerialized] private readonly List<(string ActionName, int IndexBefore)> _pendingDefaultNudges = new();

		// players
		[NonSerialized] private readonly LampPlayer _lampPlayer = new();
		[NonSerialized] private readonly CoilPlayer _coilPlayer = new();
		[NonSerialized] private readonly SwitchPlayer _switchPlayer = new();
		[NonSerialized] private readonly WirePlayer _wirePlayer = new();
		[NonSerialized] private readonly DisplayPlayer _displayPlayer = new();

		private const float SlowMotionMax = 0.1f;
		private const float TimeLapseMax = 2.5f;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private readonly LogOncePerKey<int> _unknownHittableLog = new();
		private TableComponent _tableComponent;
		private PlayfieldComponent _playfieldComponent;
		private PhysicsEngine _physicsEngine;
		private SimulationThreadComponent _simulationThreadComponent;
		private CancellationTokenSource _gamelogicEngineInitCts;

		private PlayfieldComponent PlayfieldComponent {
			get {
				if (_playfieldComponent == null) {
					_playfieldComponent = GetComponentInChildren<PlayfieldComponent>();
				}
				return _playfieldComponent;
			}
		}

		private PhysicsEngine PhysicsEngine {
			get {
				if (_physicsEngine == null) {
					_physicsEngine = GetComponentInChildren<PhysicsEngine>();
				}
				return _physicsEngine;
			}
		}

		private SimulationThreadComponent SimulationThreadComponent {
			get {
				if (_simulationThreadComponent == null) {
					_simulationThreadComponent = GetComponentInChildren<SimulationThreadComponent>(true);
				}
				return _simulationThreadComponent;
			}
		}

		private void EnsureSimulationThreadComponent()
		{
			_simulationThreadComponent = Simulation.SimulationThreadComponent.EnsureFor(PhysicsEngine);
		}

		#region Access

		internal IApiSwitch Switch(ISwitchDeviceComponent component, string switchItem) => component != null ? _switchPlayer.Switch(component, switchItem) : null;
		public IApiCoil Coil(ICoilDeviceComponent component, string coilItem) => component != null ? _coilPlayer.Coil(component, coilItem) : null;
		public IApiLamp Lamp(ILampDeviceComponent component) => component != null ? _lampPlayer.Lamp(component) : null;
		public IApiLamp Lamp(ILampDeviceComponent component, string deviceItem) => component != null ? _lampPlayer.Lamp(component, deviceItem) : null;
		public IApiWireDeviceDest WireDevice(IWireableComponent c) => _wirePlayer.WireDevice(c);
		internal void HandleWireSwitchChange(WireDestConfig wireConfig, bool isEnabled) => _wirePlayer.HandleSwitchChange(wireConfig, isEnabled);

		/// <summary>
		/// Registers a hardware-rule wire coil so the simulation thread fires it when the input action fires
		/// (low-latency flippers under native input). Returns true when the coil is now on that path.
		/// </summary>
		internal bool RegisterInputActionWireCoil(string inputActionHint, ISimulationThreadCoil coil)
			=> SimulationThreadComponent != null
				&& SimulationThreadComponent.RegisterInputActionWireCoil(inputActionHint, coil);

		/// <summary>Removes a hardware-rule wire coil registered for simulation-thread dispatch.</summary>
		internal void UnregisterInputActionWireCoil(string inputActionHint, ISimulationThreadCoil coil)
			=> SimulationThreadComponent?.UnregisterInputActionWireCoil(inputActionHint, coil);


		internal void DispatchSwitch(string switchId, bool isClosed)
		{
			if (SimulationThreadComponent != null && SimulationThreadComponent.EnqueueSwitchFromMainThread(switchId, isClosed)) {
				return;
			}
			GamelogicEngine?.Switch(switchId, isClosed);
		}

		internal void DispatchInputAction(string actionName, bool isPressed)
		{
			_switchPlayer.DispatchInputAction(actionName, isPressed);
			_wirePlayer.DispatchInputAction(actionName, isPressed);
			CabinetInputActionChanged?.Invoke(actionName, isPressed);
		}

		internal bool DispatchCoilSimulationThread(string coilId, float value)
		{
			return _coilPlayer.HandleCoilEventSimulationThread(coilId, value);
		}

		public bool SupportsSimulationThreadCoilDispatch(string coilId)
		{
			return _coilPlayer.SupportsSimulationThreadDispatch(coilId);
		}

		public Dictionary<string, IApiSwitchStatus> SwitchStatuses => _switchPlayer.SwitchStatuses;
		public Dictionary<string, bool> CoilStatuses => _coilPlayer.CoilStatuses;
		public Dictionary<string, LampState> LampStatuses => _lampPlayer.LampStates;
		public Dictionary<string, (bool, float)> WireStatuses => _wirePlayer.WireStatuses;

		public int NextBallId => ++_currentBallId;
		private int _currentBallId;

		public void SetLamp(string lampId, float value) => _lampPlayer.HandleLampEvent(lampId, value);
		public void SetLamp(string lampId, float value, LampSource source) => _lampPlayer.HandleLampEvent(lampId, value, source);
		public void SetLamp(string lampId, LampStatus status) => _lampPlayer.HandleLampEvent(lampId, status);
		public void SetLamp(string lampId, LampStatus status, LampSource source) => _lampPlayer.HandleLampEvent(lampId, status, source);
		public void SetLamp(string lampId, Color color) => _lampPlayer.HandleLampEvent(lampId, color);
		public void SetLamp(string lampId, Color color, LampSource source) => _lampPlayer.HandleLampEvent(lampId, color, source);

		#endregion

		#region Lifecycle

		private void Awake()
		{
			_tableComponent = GetComponent<TableComponent>();
			var engineComponent = GetComponent<IGamelogicEngine>();

			_apis.Add(TableApi);

			BallManager = new BallManager(GetComponentInChildren<PhysicsEngine>(), this, Playfield.transform);
			_inputManager = new InputManager();
			EnsureSimulationThreadComponent();

			// a table without a gamelogic engine still has its wires (keys to flippers) and devices
			GamelogicEngine = engineComponent;
			_lampPlayer.Awake(this, _tableComponent, GamelogicEngine);
			_coilPlayer.Awake(this, _tableComponent, GamelogicEngine, _lampPlayer, _wirePlayer);
			_switchPlayer.Awake(this, _tableComponent, GamelogicEngine, _inputManager);
			_wirePlayer.Awake(_tableComponent, _inputManager, _switchPlayer, this, PhysicsEngine);
			if (engineComponent != null) {
				_displayPlayer.Awake(GamelogicEngine);
			}
		}

		private async void Start()
		{
			#if FPS60_IOS && UNITY_IOS && !UNITY_EDITOR
				Application.targetFrameRate = 60;
			#endif

			// trigger init events now, each on its own, so one that fails can't stop the game from starting.
			foreach (var i in _apis) {
				try {
					i.OnInit(BallManager);
				} catch (Exception e) {
					Logger.Error(e, "Initializing {Api:l} failed, skipping it.", i.GetType().Name);
				}
			}

			_coilPlayer.OnStart();
			_switchPlayer.OnStart();
			_lampPlayer.OnStart();
			_wirePlayer.OnStart();
			_inputManager.Enable(HandleInput);
			InputSystem.onBeforeUpdate += OnBeforeInputUpdate;

			_gamelogicEngineInitCts = new CancellationTokenSource();
			var attempt = LoadAttempt.Current;
			var engineName = GamelogicEngine?.Name ?? "none";
			attempt?.SetPhase(LoadAttempt.PhaseGamelogic);
			PublishGamelogicDiagnostics(attempt);
			try {
				if (GamelogicEngine != null) {
					var stopwatch = System.Diagnostics.Stopwatch.StartNew();
					Logger.Info("Starting gamelogic engine {Engine}", engineName);
					await GamelogicEngine.OnInit(this, TableApi, BallManager, _gamelogicEngineInitCts.Token);
					if (!IsCurrent(attempt)) {
						// the table was exited, or another one loaded, while the engine started
						return;
					}
					if (StartupFailure(out var reason)) {
						// the engine already told the user what's wrong; the table stays playable without it
						Logger.Info("Gamelogic engine {Engine} did not start: {Reason}", engineName, reason);
						attempt?.Fail(reason);
					} else {
						Logger.Info("Gamelogic engine {Engine} started in {InitMs} ms", engineName, stopwatch.ElapsedMilliseconds);
					}
					// engines know more once they run, e.g. versions reported by an emulator
					PublishGamelogicDiagnostics(attempt);
				}
				attempt?.Succeed();
				PerformanceMonitor.BeginPlay();
				OnPlayerStarted?.Invoke(this, EventArgs.Empty);

			} catch (OperationCanceledException) {
				// the table was exited while the engine started

			} catch (Exception e) {
				// engines report what the user can fix (missing ROM...) themselves; anything
				// that escapes from here is a bug
				Logger.Error(e, "Gamelogic engine {Engine} failed to start", engineName);
				if (IsCurrent(attempt)) {
					attempt?.Fail(e);
				}
			}
		}

		/// <summary>
		/// Updates the gamelogic engine's section of the diagnostics, and fails the table's load
		/// attempt if the engine now reports a <see cref="IGamelogicStartupStatus.StartupFailure"/>.
		/// Engines call it when they learn something after <c>OnInit</c>, e.g. once an emulated game
		/// started or stopped. Main thread only.
		/// </summary>
		public void RefreshGamelogicDiagnostics()
		{
			var attempt = LoadAttempt.Current;
			if (!IsCurrent(attempt)) {
				return;
			}
			if (StartupFailure(out var reason)) {
				attempt?.Fail(reason);
			}
			PublishGamelogicDiagnostics(attempt);
		}

		private bool StartupFailure(out string reason)
		{
			reason = (GamelogicEngine as IGamelogicStartupStatus)?.StartupFailure;
			return !string.IsNullOrEmpty(reason);
		}

		/// <summary>
		/// Whether this player still runs the table the given load attempt is about.
		/// </summary>
		private bool IsCurrent(LoadAttempt attempt)
		{
			// destroyed Unity objects compare equal to null
			return this != null && LoadAttempt.Current == attempt;
		}

		/// <summary>
		/// Writes the gamelogic engine's section of the diagnostics: which engine, which game,
		/// and whatever the engine itself reports.
		/// </summary>
		private void PublishGamelogicDiagnostics(LoadAttempt attempt)
		{
			var writer = new DiagnosticsWriter();
			writer.Add("engine", GamelogicEngine?.Name ?? "none");
			if (GamelogicEngine is IRomNameProvider romNameProvider && !string.IsNullOrEmpty(romNameProvider.RomName)) {
				writer.Add("rom", romNameProvider.RomName);
			}
			if (GamelogicEngine is IGameIdentityProvider identityProvider && identityProvider.GameIdentity is { } identity) {
				writer.Add("game-id", identity.Id);
				attempt?.SetGameId(identity.Id);
			}
			if (GamelogicEngine is IDiagnosticsSource source) {
				try {
					source.Describe(writer);
				} catch (Exception e) {
					writer.Add("error", "describing failed: " + e.Message);
					Logger.Error(e, "{Engine} failed to describe itself for diagnostics", GamelogicEngine.Name);
				}
			}
			DiagnosticsContext.Set(DiagnosticsContext.Gamelogic, writer);
		}

		private void Update()
		{
			if (_pendingDefaultNudges.Count > 0) {
				foreach (var (actionName, indexBefore) in _pendingDefaultNudges) {
					if (PhysicsEngine.KeyboardNudgeIndex == indexBefore) {
						ApplyDefaultKeyboardNudge(actionName);
					}
				}
				_pendingDefaultNudges.Clear();
			}

			OnUpdate?.Invoke(this, EventArgs.Empty);
		}

		private void OnDestroy()
		{
			PerformanceMonitor.EndPlay();
			_gamelogicEngineInitCts?.Cancel();
			_gamelogicEngineInitCts?.Dispose();

			foreach (var i in _apis) {
				i.OnDestroy();
			}

			InputSystem.onBeforeUpdate -= OnBeforeInputUpdate;
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

		public void Register(PlungerApi plungerApi, PlungerComponent component, InputActionReference actionRef)
		{
			Register(plungerApi, component);
			if (actionRef != null) {
				actionRef.action.performed += plungerApi.OnAnalogPlunge;
				_actions.Add((actionRef.action, plungerApi.OnAnalogPlunge));
			}
		}

		public void Register(PlayfieldApi playfieldApi)
		{
			PlayfieldApi = playfieldApi;
			_colliderGenerators.Add(playfieldApi);
		}

		public void Register<TApi>(TApi api, MonoBehaviour component) where TApi : IApi
		{
			TableApi.Register(component, api);
			_apis.Add(api);
			var itemId = UnityObjectId.Get(component.gameObject);
			if (Logger.IsDebugEnabled) {
				Logger.Debug("Registering {Type:l} {Name} with ID {ItemId}.", component.GetType().Name, component.name, itemId);
			}
			if (api is IApiRotatable rotatable) {
				_rotatables[itemId] = rotatable;
			}
			if (api is IApiSlingshot slingshot) {
				_slingshots[itemId] = slingshot;
			}
			if (api is IApiDroppable droppable) {
				_droppables[itemId] = droppable;
			}
			if (api is IApiMagnetEvents magnetEvents) {
				_magnets[itemId] = magnetEvents;
				if (api is IApiHittable magnetHittable) {
					_hittables[itemId] = magnetHittable;
				}
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
		public void Nudge(float angleDeg, float force) => PhysicsEngine.Nudge(angleDeg, force);
		public void NudgeSensorStatus(out float x, out float y) => PhysicsEngine.NudgeSensorStatus(out x, out y);
		public void NudgeTiltStatus(out float plumbX, out float plumbY, out float tiltPercent) => PhysicsEngine.NudgeTiltStatus(out plumbX, out plumbY, out tiltPercent);
		public void NudgeGetCalibration(out float x, out float y)
		{
			x = 0f;
			y = 0f;
		}
		public void NudgeSetCalibration(float x, float y) { }

		public void OnEvent(in EventData eventData)
		{
				switch (eventData.EventId) {
				case EventId.HitEventsHit:
				case EventId.HitEventsUnhit:
					// colliders only raise hit events for items that registered a hit handler, so
					// this is a bug; report it once per item and ignore the item's hits
					if (!_hittables.TryGetValue(eventData.ItemId, out var hittable)) {
						if (_unknownHittableLog.ShouldLog(eventData.ItemId)) {
							Logger.Error("A ball hit item {ItemId}, which has no hit handler. Ignoring its hits.", eventData.ItemId);
						}
						break;
					}
					hittable.OnHit(eventData.BallId, eventData.EventId == EventId.HitEventsUnhit);
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

				case EventId.MagnetEventsBallEntered:
					_magnets[eventData.ItemId].OnMagnetBallEntered(eventData.BallId);
					break;

				case EventId.MagnetEventsBallExited:
					_magnets[eventData.ItemId].OnMagnetBallExited(eventData.BallId);
					break;

				case EventId.MagnetEventsBallGrabbed:
					_magnets[eventData.ItemId].OnMagnetBallGrabbed(eventData.BallId);
					break;

				case EventId.MagnetEventsBallReleased:
					_magnets[eventData.ItemId].OnMagnetBallReleased(eventData.BallId);
					break;

				case EventId.PhysicsDiagnosticsUnsupportedOwnedInteraction:
					Logger.Warn($"Owned magnet {eventData.ItemId} released ball {eventData.BallId} before unsupported active interaction with item {eventData.IntParam}.");
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
			_wirePlayer.AddWire(wireMapping, isHardwareRule: true);

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

		private void HandleInput(object obj, InputActionChange change)
		{
			if (obj is not InputAction action || action.actionMap == null) {
				return;
			}

			if (action.actionMap.name == InputConstants.MapCabinetSwitches) {
				HandleCabinetInputAction(action, change);
				HandleNudgeInput(action, change);
			}

			if (action.actionMap.name == InputConstants.MapDebug) {
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

		private void HandleCabinetInputAction(InputAction action, InputActionChange change)
		{
			if (NativeInputManager.TryGetExistingInstance()?.IsPolling == true) {
				return;
			}

			if (change != InputActionChange.ActionStarted && change != InputActionChange.ActionCanceled) {
				return;
			}

			CabinetInputActionChanged?.Invoke(InputManager.GetCanonicalActionName(action.name),
				change == InputActionChange.ActionStarted);
		}

		private void HandleNudgeInput(InputAction action, InputActionChange change)
		{
			if (NativeInputManager.TryGetExistingInstance()?.IsPolling == true || change != InputActionChange.ActionStarted) {
				return;
			}

			var actionName = InputManager.GetCanonicalActionName(action.name);
			if (actionName != InputConstants.ActionLeftNudge
			    && actionName != InputConstants.ActionRightNudge
			    && actionName != InputConstants.ActionCenterNudge) {
				return;
			}

			_pendingDefaultNudges.Add((actionName, _nudgeIndexAtInputUpdateStart));
		}

		private void OnBeforeInputUpdate()
		{
			if (PhysicsEngine != null) {
				_nudgeIndexAtInputUpdateStart = PhysicsEngine.KeyboardNudgeIndex;
			}
		}

		private void ApplyDefaultKeyboardNudge(string actionName)
		{
			const float baseForce = 2f;
			var angle = ((float)_nudgeRandom.NextDouble() - 0.5f) * 15f * baseForce;
			var force = (0.6f + (float)_nudgeRandom.NextDouble() * 0.8f) * baseForce;
			switch (actionName) {
				case InputConstants.ActionLeftNudge:
					Nudge(75f + angle, force);
					break;
				case InputConstants.ActionRightNudge:
					Nudge(285f + angle, force);
					break;
				case InputConstants.ActionCenterNudge:
					Nudge(angle, force);
					break;
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
