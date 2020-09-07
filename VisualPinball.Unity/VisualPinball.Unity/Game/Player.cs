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
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity
{
	public class Player : MonoBehaviour
	{
		public Table Table { get; private set; }

		// shortcuts
		public Matrix4x4 TableToWorld => transform.localToWorldMatrix;

		// table related
		private BallManager _ballManager;
		private readonly TableApi _tableApi = new TableApi();
		private readonly List<IApiInitializable> _initializables = new List<IApiInitializable>();
		private readonly Dictionary<Entity, IApiHittable> _hittables = new Dictionary<Entity, IApiHittable>();
		private readonly Dictionary<Entity, IApiRotatable> _rotatables = new Dictionary<Entity, IApiRotatable>();
		private readonly Dictionary<Entity, IApiCollidable> _collidables = new Dictionary<Entity, IApiCollidable>();
		private readonly Dictionary<Entity, IApiSpinnable> _spinnables = new Dictionary<Entity, IApiSpinnable>();
		private readonly Dictionary<Entity, IApiSlingshot> _slingshots = new Dictionary<Entity, IApiSlingshot>();

		public Player()
		{
			_initializables.Add(_tableApi);
		}

		#region Lifecycle

		private void Awake()
		{
			var tableComponent = gameObject.GetComponent<TableAuthoring>();
			Table = tableComponent.CreateTable();
			_ballManager = new BallManager(Table, TableToWorld);
		}

		private void Start()
		{
			// bootstrap table script(s)
			var tableScripts = GetComponents<VisualPinballScript>();
			foreach (var tableScript in tableScripts) {
				tableScript.OnAwake(_tableApi, _ballManager);
			}

			// trigger init events now
			foreach (var i in _initializables) {
				i.OnInit();
			}
		}

		private void Update()
		{
			// flippers will be handled via script later, but until scripting works, do it here.
			if (Keyboard.current.leftShiftKey.wasPressedThisFrame) { 
				_tableApi.Flipper("LeftFlipper")?.RotateToEnd();
			}
			if (Keyboard.current.leftShiftKey.wasReleasedThisFrame) {
				_tableApi.Flipper("LeftFlipper")?.RotateToStart();
			}
			if (Keyboard.current.rightShiftKey.wasPressedThisFrame) {
				_tableApi.Flipper("RightFlipper")?.RotateToEnd();
			}
			if (Keyboard.current.rightShiftKey.wasReleasedThisFrame) {
				_tableApi.Flipper("RightFlipper")?.RotateToStart();
			}

			if (Keyboard.current.bKey.wasReleasedThisFrame) {
				_ballManager.CreateBall(new DebugBallCreator());
			}

			if (Keyboard.current.nKey.wasReleasedThisFrame) {
				//_ballManager.CreateBall(new DebugBallCreator(Table.Width / 2f, Table.Height / 2f - 300f, 0, -5));
				_tableApi.Kicker("Kicker1").CreateBall();
				_tableApi.Kicker("Kicker1").Kick(0, -1);
			}

			if (Keyboard.current.enterKey.wasPressedThisFrame) {
				_tableApi.Plunger("Plunger")?.PullBack();
			}
			if (Keyboard.current.enterKey.wasReleasedThisFrame) {
				_tableApi.Plunger("Plunger")?.Fire();
			}
		}

		#endregion

		#region Registrations

		public void RegisterBumper(Bumper bumper, Entity entity, GameObject go)
		{
			var bumperApi = new BumperApi(bumper, entity, this);
			_tableApi.Bumpers[bumper.Name] = bumperApi;
			_initializables.Add(bumperApi);
			_hittables[entity] = bumperApi;
		}

		public void RegisterFlipper(Flipper flipper, Entity entity, GameObject go)
		{
			var flipperApi = new FlipperApi(flipper, entity, this);
			_tableApi.Flippers[flipper.Name] = flipperApi;
			_initializables.Add(flipperApi);
			_hittables[entity] = flipperApi;
			_rotatables[entity] = flipperApi;
			_collidables[entity] = flipperApi;

			if (EngineProvider<IDebugUI>.Exists) {
				EngineProvider<IDebugUI>.Get().OnRegisterFlipper(entity, flipper.Name);
			}
		}

		public void RegisterGate(Gate gate, Entity entity, GameObject go)
		{
			var gateApi = new GateApi(gate, entity, this);
			_tableApi.Gates[gate.Name] = gateApi;
			_initializables.Add(gateApi);
			_hittables[entity] = gateApi;
			_rotatables[entity] = gateApi;
		}

		public void RegisterHitTarget(HitTarget hitTarget, Entity entity, GameObject go)
		{
			var hitTargetApi = new HitTargetApi(hitTarget, entity, this);
			_tableApi.HitTargets[hitTarget.Name] = hitTargetApi;
			_initializables.Add(hitTargetApi);
			_hittables[entity] = hitTargetApi;
		}

		public void RegisterKicker(Kicker kicker, Entity entity, GameObject go)
		{
			var kickerApi = new KickerApi(kicker, entity, this);
			_tableApi.Kickers[kicker.Name] = kickerApi;
			_initializables.Add(kickerApi);
			_hittables[entity] = kickerApi;
		}

		public void RegisterPlunger(Plunger plunger, Entity entity, GameObject go)
		{
			var plungerApi = new PlungerApi(plunger, entity, this);
			_tableApi.Plungers[plunger.Name] = plungerApi;
			_initializables.Add(plungerApi);
			_rotatables[entity] = plungerApi;
		}

		public void RegisterRamp(Ramp ramp, Entity entity, GameObject go)
		{
			var rampApi = new RampApi(ramp, entity, this);
			_tableApi.Ramps[ramp.Name] = rampApi;
			_initializables.Add(rampApi);
		}

		public void RegisterRubber(Rubber rubber, Entity entity, GameObject go)
		{
			var rubberApi = new RubberApi(rubber, entity, this);
			_tableApi.Rubbers[rubber.Name] = rubberApi;
			_initializables.Add(rubberApi);
			_hittables[entity] = rubberApi;
		}

		public void RegisterSurface(Surface surface, Entity entity, GameObject go)
		{
			var surfaceApi = new SurfaceApi(surface, entity, this);
			_tableApi.Surfaces[surface.Name] = surfaceApi;
			_initializables.Add(surfaceApi);
			_hittables[entity] = surfaceApi;
			_slingshots[entity] = surfaceApi;
		}

		public void RegisterSpinner(Spinner spinner, Entity entity, GameObject go)
		{
			var spinnerApi = new SpinnerApi(spinner, entity, this);
			_tableApi.Spinners[spinner.Name] = spinnerApi;
			_initializables.Add(spinnerApi);
			_spinnables[entity] = spinnerApi;
			_rotatables[entity] = spinnerApi;
		}

		public void RegisterTrigger(Trigger trigger, Entity entity, GameObject go)
		{
			var triggerApi = new TriggerApi(trigger, entity, this);
			_tableApi.Triggers[trigger.Name] = triggerApi;
			_initializables.Add(triggerApi);
			_hittables[entity] = triggerApi;
		}

		public void RegisterPrimitive(Primitive primitive, Entity entity, GameObject go)
		{
			var primitiveApi = new PrimitiveApi(primitive, entity, this);
			_tableApi.Primitives[primitive.Name] = primitiveApi;
			_initializables.Add(primitiveApi);
			_hittables[entity] = primitiveApi;
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
					_hittables[eventData.ItemEntity].OnHit();
					break;

				case EventId.HitEventsUnhit:
					_hittables[eventData.ItemEntity].OnHit(true);
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
					_collidables[eventData.ItemEntity].OnCollide(eventData.FloatParam);
					break;

				case EventId.SurfaceEventsSlingshot:
					_slingshots[eventData.ItemEntity].OnSlingshot();
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
