// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using NLog;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Engine.VPT.Trough;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class Player : MonoBehaviour
	{
		public Table Table { get; private set; }
		public TableApi TableApi { get; }

		// shortcuts
		public Matrix4x4 TableToWorld => transform.localToWorldMatrix;

		[NonSerialized]
		public IGamelogicEngine GameEngine;

		[NonSerialized]
		public BallManager BallManager;

		[HideInInspector] [SerializeField] public string debugUiId;
		[HideInInspector] [SerializeField] public string physicsEngineId;

		// table related
		private readonly List<IApi> _apis = new List<IApi>();
		private readonly List<IApiInitializable> _initializables = new List<IApiInitializable>();
		private readonly Dictionary<Entity, IApiHittable> _hittables = new Dictionary<Entity, IApiHittable>();
		private readonly Dictionary<Entity, IApiRotatable> _rotatables = new Dictionary<Entity, IApiRotatable>();
		private readonly Dictionary<Entity, IApiCollidable> _collidables = new Dictionary<Entity, IApiCollidable>();
		private readonly Dictionary<Entity, IApiSpinnable> _spinnables = new Dictionary<Entity, IApiSpinnable>();
		private readonly Dictionary<Entity, IApiSlingshot> _slingshots = new Dictionary<Entity, IApiSlingshot>();
		private readonly Dictionary<string, IApiSwitch> _switches = new Dictionary<string, IApiSwitch>();
		private readonly Dictionary<string, IApiSwitchDevice> _switchDevices = new Dictionary<string, IApiSwitchDevice>();
		private readonly Dictionary<string, IApiCoil> _coils = new Dictionary<string, IApiCoil>();
		private readonly Dictionary<string, IApiCoilDevice> _coilDevices = new Dictionary<string, IApiCoilDevice>();
		private readonly Dictionary<string, IApiWireDest> _wires = new Dictionary<string, IApiWireDest>();
		private readonly Dictionary<string, IApiWireDeviceDest> _wireDevices = new Dictionary<string, IApiWireDeviceDest>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		// input related
		private InputManager _inputManager;

		[NonSerialized]
		private readonly Dictionary<string, List<string>> _keySwitchAssignments = new Dictionary<string, List<string>>();
		private readonly Dictionary<string, List<WireDestConfig>> _keyWireAssignments = new Dictionary<string, List<WireDestConfig>>();
		private readonly Dictionary<string, List<Tuple<string, bool, string>>> _coilAssignments = new Dictionary<string, List<Tuple<string, bool, string>>>();

		public Player()
		{
			TableApi = new TableApi(this);
			_initializables.Add(TableApi);
		}

		#region Access

		internal IApiSwitch Switch(string n) => _switches.ContainsKey(n) ? _switches[n] : null;
		internal IApiWireDest Wire(string n) => _wires.ContainsKey(n) ? _wires[n] : null;
		internal IApiWireDeviceDest WireDevice(string n) => _wireDevices.ContainsKey(n) ? _wireDevices[n] : null;

		#endregion


		#region Lifecycle

		private void Awake()
		{
			var tableComponent = gameObject.GetComponent<TableAuthoring>();
			var engineComponent = GetComponent<IGameEngineAuthoring>();

			Table = tableComponent.CreateTable(tableComponent.Data);
			BallManager = new BallManager(Table, TableToWorld);
			_inputManager = new InputManager();

			if (engineComponent != null) {
				GameEngine = engineComponent.GameEngine;
			}

			EngineProvider<IPhysicsEngine>.Set(physicsEngineId);
			EngineProvider<IPhysicsEngine>.Get().Init(tableComponent, BallManager);
			if (!string.IsNullOrEmpty(debugUiId)) {
				EngineProvider<IDebugUI>.Set(debugUiId);
			}
		}

		private void Update()
		{
			GameEngine?.OnUpdate();
		}

		private void OnDestroy()
		{
			if (_keySwitchAssignments.Count > 0) {
				_inputManager.Disable(HandleKeyInput);
			}
			if (_coilAssignments.Count > 0 && GameEngine is IGamelogicEngineWithCoils gamelogicEngineWithCoils) {
				gamelogicEngineWithCoils.OnCoilChanged -= HandleCoilEvent;
			}

			foreach (var i in _apis) {
				i.OnDestroy();
			}

			GameEngine?.OnDestroy();
		}

		private void Start()
		{

			// bootstrap table script(s)
			var tableScripts = GetComponents<VisualPinballScript>();
			foreach (var tableScript in tableScripts) {
				tableScript.OnAwake(TableApi, BallManager);
			}

			// trigger init events now
			foreach (var i in _initializables) {
				i.OnInit(BallManager);
			}

			// hook up mapping configuration
			SetupSwitchMapping();
			SetupCoilMapping();
			SetupWireMapping();

			GameEngine?.OnInit(TableApi, BallManager);
		}

		#endregion

		#region Registrations

		public void RegisterBumper(Bumper bumper, Entity entity, GameObject go)
		{
			var bumperApi = new BumperApi(bumper, entity, this);
			TableApi.Bumpers[bumper.Name] = bumperApi;
			_apis.Add(bumperApi);
			_initializables.Add(bumperApi);
			_hittables[entity] = bumperApi;
			_switches[bumper.Name] = bumperApi;
			_coils[bumper.Name] = bumperApi;
			_wires[bumper.Name] = bumperApi;
		}

		public void RegisterFlipper(Flipper flipper, Entity entity, GameObject go)
		{
			var flipperApi = new FlipperApi(flipper, entity, this);
			TableApi.Flippers[flipper.Name] = flipperApi;
			_apis.Add(flipperApi);
			_initializables.Add(flipperApi);
			_hittables[entity] = flipperApi;
			_rotatables[entity] = flipperApi;
			_collidables[entity] = flipperApi;
			_switches[flipper.Name] = flipperApi;
			_coils[flipper.Name] = flipperApi;
			_wires[flipper.Name] = flipperApi;

			if (EngineProvider<IDebugUI>.Exists) {
				EngineProvider<IDebugUI>.Get().OnRegisterFlipper(entity, flipper.Name);
			}
		}

		public void RegisterGate(Gate gate, Entity entity, GameObject go)
		{
			var gateApi = new GateApi(gate, entity, this);
			TableApi.Gates[gate.Name] = gateApi;
			_apis.Add(gateApi);
			_initializables.Add(gateApi);
			_hittables[entity] = gateApi;
			_rotatables[entity] = gateApi;
			_switches[gate.Name] = gateApi;
		}

		public void RegisterHitTarget(HitTarget hitTarget, Entity entity, GameObject go)
		{
			var hitTargetApi = new HitTargetApi(hitTarget, entity, this);
			TableApi.HitTargets[hitTarget.Name] = hitTargetApi;
			_apis.Add(hitTargetApi);
			_initializables.Add(hitTargetApi);
			_hittables[entity] = hitTargetApi;
			_switches[hitTarget.Name] = hitTargetApi;
		}

		public void RegisterKicker(Kicker kicker, Entity entity, GameObject go)
		{
			var kickerApi = new KickerApi(kicker, entity, this);
			TableApi.Kickers[kicker.Name] = kickerApi;
			_apis.Add(kickerApi);
			_initializables.Add(kickerApi);
			_hittables[entity] = kickerApi;
			_switches[kicker.Name] = kickerApi;
			_coils[kicker.Name] = kickerApi;
			_wires[kicker.Name] = kickerApi;
		}

		public void RegisterPlunger(Plunger plunger, Entity entity, GameObject go)
		{
			var plungerApi = new PlungerApi(plunger, entity, this);
			TableApi.Plungers[plunger.Name] = plungerApi;
			_apis.Add(plungerApi);
			_initializables.Add(plungerApi);
			_rotatables[entity] = plungerApi;
			_coils[plunger.Name] = plungerApi;
			_wires[plunger.Name] = plungerApi;
		}

		public void RegisterPrimitive(Primitive primitive, Entity entity, GameObject go)
		{
			var primitiveApi = new PrimitiveApi(primitive, entity, this);
			TableApi.Primitives[primitive.Name] = primitiveApi;
			_apis.Add(primitiveApi);
			_initializables.Add(primitiveApi);
			_hittables[entity] = primitiveApi;
		}

		public void RegisterRamp(Ramp ramp, Entity entity, GameObject go)
		{
			var rampApi = new RampApi(ramp, entity, this);
			TableApi.Ramps[ramp.Name] = rampApi;
			_apis.Add(rampApi);
			_initializables.Add(rampApi);
		}

		public void RegisterRubber(Rubber rubber, Entity entity, GameObject go)
		{
			var rubberApi = new RubberApi(rubber, entity, this);
			TableApi.Rubbers[rubber.Name] = rubberApi;
			_apis.Add(rubberApi);
			_initializables.Add(rubberApi);
			_hittables[entity] = rubberApi;
		}

		public void RegisterSurface(Surface surface, Entity entity, GameObject go)
		{
			var surfaceApi = new SurfaceApi(surface, entity, this);
			TableApi.Surfaces[surface.Name] = surfaceApi;
			_apis.Add(surfaceApi);
			_initializables.Add(surfaceApi);
			_hittables[entity] = surfaceApi;
			_slingshots[entity] = surfaceApi;
		}

		public void RegisterSpinner(Spinner spinner, Entity entity, GameObject go)
		{
			var spinnerApi = new SpinnerApi(spinner, entity, this);
			TableApi.Spinners[spinner.Name] = spinnerApi;
			_apis.Add(spinnerApi);
			_initializables.Add(spinnerApi);
			_spinnables[entity] = spinnerApi;
			_rotatables[entity] = spinnerApi;
			_switches[spinner.Name] = spinnerApi;
		}

		public void RegisterTrigger(Trigger trigger, Entity entity, GameObject go)
		{
			var triggerApi = new TriggerApi(trigger, entity, this);
			TableApi.Triggers[trigger.Name] = triggerApi;
			_apis.Add(triggerApi);
			_initializables.Add(triggerApi);
			_hittables[entity] = triggerApi;
			_switches[trigger.Name] = triggerApi;
		}

		public void RegisterTrough(Trough trough, GameObject go)
		{
			var troughApi = new TroughApi(trough, this);
			TableApi.Troughs[trough.Name] = troughApi;
			_apis.Add(troughApi);
			_initializables.Add(troughApi);
			_switchDevices[trough.Name] = troughApi;
			_coilDevices[trough.Name] = troughApi;
			_wireDevices[trough.Name] = troughApi;
		}

		#endregion

		#region Mappings

		private void SetupCoilMapping()
		{
			if (GameEngine is IGamelogicEngineWithCoils gamelogicEngineWithCoils) {
				var config = Table.Mappings;
				_coilAssignments.Clear();
				foreach (var coilData in config.Data.Coils) {
					switch (coilData.Destination) {
						case CoilDestination.Playfield:
							if (!_coilAssignments.ContainsKey(coilData.Id)) {
								_coilAssignments[coilData.Id] = new List<Tuple<string, bool, string>>();
							}
							_coilAssignments[coilData.Id].Add(new Tuple<string, bool, string>(coilData.PlayfieldItem, false, null));
							if (coilData.Type == CoilType.DualWound) {
								if (!_coilAssignments.ContainsKey(coilData.HoldCoilId)) {
									_coilAssignments[coilData.HoldCoilId] = new List<Tuple<string, bool, string>>();
								}
								_coilAssignments[coilData.HoldCoilId].Add(new Tuple<string, bool, string>(coilData.PlayfieldItem, true, null));
							}
							break;

						case CoilDestination.Device:
							if (_coilDevices.ContainsKey(coilData.Device)) {
								var device = _coilDevices[coilData.Device];
								var coil = device.Coil(coilData.DeviceItem);
								if (coil != null) {
									if (!_coilAssignments.ContainsKey(coilData.Id)) {
										_coilAssignments[coilData.Id] = new List<Tuple<string, bool, string>>();
									}
									_coilAssignments[coilData.Id].Add(new Tuple<string, bool, string>(coilData.DeviceItem, false, coilData.Device));

								} else {
									Logger.Warn($"Unknown coil \"{coilData.DeviceItem}\" in coil device \"{coilData.Device}\".");
								}
							}
							break;
					}
				}

				if (_coilAssignments.Count > 0) {
					gamelogicEngineWithCoils.OnCoilChanged += HandleCoilEvent;
				}
			}
		}

		private void SetupSwitchMapping()
		{
			// hook-up game switches
			if (GameEngine is IGamelogicEngineWithSwitches) {

				var config = Table.Mappings;
				_keySwitchAssignments.Clear();
				foreach (var switchData in config.Data.Switches) {
					switch (switchData.Source) {

						case SwitchSource.Playfield
							when !string.IsNullOrEmpty(switchData.PlayfieldItem)
							     && _switches.ContainsKey(switchData.PlayfieldItem):
						{
							var element = _switches[switchData.PlayfieldItem];
							element.AddSwitchId(new SwitchConfig(switchData));
							break;
						}

						case SwitchSource.InputSystem:
							if (!_keySwitchAssignments.ContainsKey(switchData.InputAction)) {
								_keySwitchAssignments[switchData.InputAction] = new List<string>();
							}
							_keySwitchAssignments[switchData.InputAction].Add(switchData.Id);
							break;

						case SwitchSource.Playfield:
							Logger.Warn($"Cannot find switch \"{switchData.PlayfieldItem}\" on playfield!");
							break;

						case SwitchSource.Device
							when !string.IsNullOrEmpty(switchData.Device)
							     && _switchDevices.ContainsKey(switchData.Device):
						{
							var device = _switchDevices[switchData.Device];
							var deviceSwitch = device.Switch(switchData.DeviceItem);
							if (deviceSwitch != null) {
								deviceSwitch.AddSwitchId(new SwitchConfig(switchData));

							} else {
								Logger.Warn($"Unknown switch \"{switchData.DeviceItem}\" in switch device \"{switchData.Device}\".");
							}
							break;
						}
						case SwitchSource.Device when string.IsNullOrEmpty(switchData.Device):
							Logger.Warn($"Switch device not set for switch \"{switchData.Id}\".");
							break;

						case SwitchSource.Device when !_switchDevices.ContainsKey(switchData.Device):
							Logger.Warn($"Unknown switch device \"{switchData.Device}\" for switch \"{switchData.Id}\".");
							break;

						case SwitchSource.Constant:
							break;

						default:
							Logger.Warn($"Unknown switch source \"{switchData.Source}\".");
							break;
					}
				}

				if (_keySwitchAssignments.Count > 0) {
					_inputManager.Enable(HandleKeyInput);
				}
			}
		}

		private void SetupWireMapping()
		{
			var config = Table.Mappings;
			_keyWireAssignments.Clear();
			foreach (var wireData in config.Data.Wires) {
				switch (wireData.Source) {

					case SwitchSource.Playfield
						when !string.IsNullOrEmpty(wireData.SourcePlayfieldItem)
						     && _switches.ContainsKey(wireData.SourcePlayfieldItem):
					{
						_switches[wireData.SourcePlayfieldItem].AddWireDest(new WireDestConfig(wireData));
						break;
					}

					case SwitchSource.InputSystem:
						if (!_keyWireAssignments.ContainsKey(wireData.SourceInputAction)) {
							_keyWireAssignments[wireData.SourceInputAction] = new List<WireDestConfig>();
						}
						_keyWireAssignments[wireData.SourceInputAction].Add(new WireDestConfig(wireData));
						break;

					case SwitchSource.Playfield:
						Logger.Warn($"Cannot find wire switch \"{wireData.Src}\" on playfield!");
						break;

					case SwitchSource.Device
						when !string.IsNullOrEmpty(wireData.SourceDevice)
						     && _switchDevices.ContainsKey(wireData.SourceDevice):
					{
						var device = _switchDevices[wireData.SourceDevice];
						var deviceSwitch = device.Switch(wireData.SourceDeviceItem);
						if (deviceSwitch != null) {
							deviceSwitch.AddWireDest(new WireDestConfig(wireData));
							Logger.Info($"Wiring device switch \"{wireData.Src}\" to \"{wireData.Dst}\"");

						} else {
							Logger.Warn($"Unknown switch \"{wireData.Src}\" to wire to \"{wireData.Dst}\".");
						}
						break;
					}
					case SwitchSource.Device when string.IsNullOrEmpty(wireData.SourceDevice):
						Logger.Warn($"Switch device not set for switch \"{wireData.Src}\".");
						break;

					case SwitchSource.Device when !_switchDevices.ContainsKey(wireData.SourceDevice):
						Logger.Warn($"Unknown switch device \"{wireData.SourceDevice}\" to wire to \"{wireData.Dst}\".");
						break;

					case SwitchSource.Constant:
						break;

					default:
						Logger.Warn($"Unknown wire switch source \"{wireData.Source}\".");
						break;
				}
			}

			if (_keySwitchAssignments.Count > 0) {
				_inputManager.Enable(HandleKeyInput);
			}
		}

		private void HandleKeyInput(object obj, InputActionChange change)
		{
			switch (change) {
				case InputActionChange.ActionStarted:
				case InputActionChange.ActionCanceled:
					var action = (InputAction) obj;
					if (_keySwitchAssignments.ContainsKey(action.name)) {
						if (GameEngine is IGamelogicEngineWithSwitches engineWithSwitches) {
							foreach (var switchId in _keySwitchAssignments[action.name]) {
								engineWithSwitches.Switch(switchId, change == InputActionChange.ActionStarted);
							}
						}
					} else {
						Logger.Info($"Unmapped input command \"{action.name}\".");
					}

					if (_keyWireAssignments != null && _keyWireAssignments.ContainsKey(action.name)) {
						foreach (var wireConfig in _keyWireAssignments[action.name]) {
							switch (wireConfig.Destination) {
								case WireDestination.Playfield:
									Wire(wireConfig.PlayfieldItem)?.OnChange(change == InputActionChange.ActionStarted);
									break;

								case WireDestination.Device:
									break;
							}
						}
					}
					break;
			}
		}

		private void HandleCoilEvent(object sender, CoilEventArgs coilEvent)
		{
			if (_coilAssignments.ContainsKey(coilEvent.Id)) {
				foreach (var (itemName, isHoldCoil, deviceName) in _coilAssignments[coilEvent.Id]) {
					if (deviceName != null && _coilDevices.ContainsKey(deviceName)) {
						_coilDevices[deviceName].Coil(itemName).OnCoil(coilEvent.IsEnabled, isHoldCoil);

					} else if (_coils.ContainsKey(itemName)) {
						_coils[itemName].OnCoil(coilEvent.IsEnabled, isHoldCoil);

					} else {
						Logger.Warn($"Cannot trigger unknown coil item {itemName}.");
					}
				}

			} else {
				var what = coilEvent.IsEnabled ? "turn on" : "turn off";
				Logger.Warn($"Should {what} unassigned coil {coilEvent.Id}.");
			}
		}

		#endregion

		#region Events

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

		public float3 GetGravity()
		{
			var slope = Table.Data.AngleTiltMin + (Table.Data.AngleTiltMax - Table.Data.AngleTiltMin) * Table.Data.GlobalDifficulty;
			var strength = Table.Data.OverridePhysics != 0 ? PhysicsConstants.DefaultTableGravity : Table.Data.Gravity;
			return new float3(0,  math.sin(math.radians(slope)) * strength, -math.cos(math.radians(slope)) * strength);
		}
	}
}
