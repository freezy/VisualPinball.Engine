using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Physics.DebugUI;
using VisualPinball.Unity.Physics.Event;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.VPT.Ball;
using VisualPinball.Unity.VPT.Flipper;
using VisualPinball.Unity.VPT.Gate;
using VisualPinball.Unity.VPT.Kicker;
using VisualPinball.Unity.VPT.Plunger;
using VisualPinball.Unity.VPT.Rubber;
using VisualPinball.Unity.VPT.Surface;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Game
{
	public class Player : MonoBehaviour
	{
		// table related
		private Table _table;
		private BallManager _ballManager;
		private readonly TableApi _tableApi = new TableApi();
		private readonly List<IApiInitializable> _initializables = new List<IApiInitializable>();
		private readonly Dictionary<Entity, IApiHittable> _hittables = new Dictionary<Entity, IApiHittable>();

		// game items
		internal readonly Dictionary<Entity, FlipperApi> Flippers = new Dictionary<Entity, FlipperApi>();
		internal readonly Dictionary<Entity, GateApi> Gates = new Dictionary<Entity, GateApi>();

		// shortcuts
		public Matrix4x4 TableToWorld => transform.localToWorldMatrix;

		public Player()
		{
			_initializables.Add(_tableApi);
		}

		#region Registrations

		public void RegisterFlipper(Flipper flipper, Entity entity, GameObject go)
		{
			var flipperApi = new FlipperApi(flipper, entity, this);

			Flippers[entity] = flipperApi;
			_tableApi.Flippers[flipper.Name] = flipperApi;
			_hittables[entity] = flipperApi;
			_initializables.Add(flipperApi);

			if (EngineProvider<IDebugUI>.Exists) {
				EngineProvider<IDebugUI>.Get().OnRegisterFlipper(entity, flipper.Name);
			}
		}

		public void RegisterGate(Gate gate, Entity entity, GameObject go)
		{
			var gateApi = new GateApi(gate, entity, this);

			Gates[entity] = gateApi;
			_tableApi.Gates[gate.Name] = gateApi;
			_hittables[entity] = gateApi;
			_initializables.Add(gateApi);
		}

		public void RegisterKicker(Kicker kicker, Entity entity, GameObject go)
		{
			var kickerApi = new KickerApi(kicker, entity, this);

			_tableApi.Kickers[kicker.Name] = kickerApi;
		}

		public void RegisterPlunger(Plunger plunger, Entity entity, GameObject go)
		{
			var plungerApi = new PlungerApi(plunger, entity, this);
			_tableApi.Plungers[plunger.Name] = plungerApi;
		}

		public void RegisterSurface(Surface surface, Entity entity, GameObject go)
		{
			var surfaceApi = new SurfaceApi(surface, entity, this);
			_hittables[entity] = surfaceApi;
			_initializables.Add(surfaceApi);
			_tableApi.Surfaces[surface.Name] = surfaceApi;
		}

		public void RegisterRubber(Rubber rubber, Entity entity, GameObject go)
		{
			var rubberApi = new RubberApi(rubber, entity, this);
			_hittables[entity] = rubberApi;
			_initializables.Add(rubberApi);
			_tableApi.Rubbers[rubber.Name] = rubberApi;
		}

		#endregion

		public void OnEvent(EventData eventData)
		{
			switch (eventData.Type) {
				case VisualPinball.Engine.Game.Event.HitEventsHit:
					if (_hittables.ContainsKey(eventData.ItemEntity)) {
						_hittables[eventData.ItemEntity].OnHit();
					}
					else {
						Debug.Log("No hittable of entity " + eventData.ItemEntity + " found.");
					}
					break;

				case VisualPinball.Engine.Game.Event.FlipperEventsCollide:
					Flippers[eventData.ItemEntity].OnCollide(eventData.FloatParam);
					break;
			}
		}

		public BallApi CreateBall(IBallCreationPosition ballCreator, float radius = 25, float mass = 1)
		{
			// todo callback and other stuff
			return _ballManager.CreateBall(this, ballCreator, radius, mass);
		}

		public float3 GetGravity()
		{
			var slope = _table.Data.AngleTiltMin + (_table.Data.AngleTiltMax - _table.Data.AngleTiltMin) * _table.Data.GlobalDifficulty;
			var strength = _table.Data.OverridePhysics != 0 ? PhysicsConstants.DefaultTableGravity : _table.Data.Gravity;
			return new float3(0,  math.sin(math.radians(slope)) * strength, -math.cos(math.radians(slope)) * strength);
		}

		private void Awake()
		{
			var tableComponent = gameObject.GetComponent<TableBehavior>();
			_table = tableComponent.CreateTable();
			_ballManager = new BallManager(_table);
		}

		private void Start()
		{
			// bootstrap table script(s)
			var tableScripts = GetComponents<VisualPinballScript>();
			foreach (var tableScript in tableScripts) {
				tableScript.OnAwake(_tableApi);
			}

			// trigger init events now
			foreach (var i in _initializables) {
				i.OnInit();
			}
		}

		private void Update()
		{
			// flippers will be handled via script later, but until scripting works, do it here.
			if (Input.GetKeyDown("left shift")) {
				_tableApi.Flipper("LeftFlipper")?.RotateToEnd();
			}
			if (Input.GetKeyUp("left shift")) {
				_tableApi.Flipper("LeftFlipper")?.RotateToStart();
			}
			if (Input.GetKeyDown("right shift")) {
				_tableApi.Flipper("RightFlipper")?.RotateToEnd();
			}
			if (Input.GetKeyUp("right shift")) {
				_tableApi.Flipper("RightFlipper")?.RotateToStart();
			}

			if (Input.GetKeyUp("b")) {
				CreateBall(new DebugBallCreator());
				// _player.CreateBall(new DebugBallCreator(425, 1325));
				// _player.CreateBall(new DebugBallCreator(390, 1125));

				// _player.CreateBall(new DebugBallCreator(475, 1727.5f));
				// _tableApi.Flippers["RightFlipper"].RotateToEnd();
			}

			if (Input.GetKeyUp("n")) {
				CreateBall(new DebugBallCreator(278.4076f, 1742.555f));
				//_tableApi.Flippers["LeftFlipper"].RotateToEnd();
			}

			if (Input.GetKeyDown(KeyCode.Return)) {
				_tableApi.Plunger("CustomPlunger")?.PullBack();
				_tableApi.Plunger("Plunger001")?.PullBack();
				_tableApi.Plunger("Plunger002")?.PullBack();
			}
			if (Input.GetKeyUp(KeyCode.Return)) {
				_tableApi.Plunger("CustomPlunger")?.Fire();
				_tableApi.Plunger("Plunger001")?.Fire();
				_tableApi.Plunger("Plunger002")?.Fire();
			}
		}
	}
}
