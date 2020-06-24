using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ball;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Physics.DebugUI;
using VisualPinball.Unity.VPT.Ball;
using VisualPinball.Unity.VPT.Flipper;
using VisualPinball.Unity.VPT.Kicker;
using VisualPinball.Unity.VPT.Plunger;
using VisualPinball.Unity.VPT.Table;
using Random = UnityEngine.Random;

namespace VisualPinball.Unity.Game
{
	public class Player : MonoBehaviour
	{
		private readonly TableApi _tableApi = new TableApi();

		private readonly Dictionary<int, FlipperApi> _flippers = new Dictionary<int, FlipperApi>();
		private FlipperApi Flipper(int entityIndex) => _flippers.Values.FirstOrDefault(f => f.Entity.Index == entityIndex);

		#if FLIPPER_LOG
		public static StreamWriter DebugLog;
		#endif

		private Table _table;
		private BallManager _ballManager;
		private Player _player;

		public Matrix4x4 TableToWorld => transform.localToWorldMatrix;

		public void RegisterFlipper(Flipper flipper, Entity entity, GameObject go)
		{
			//AttachToRoot(entity, go);
			var flipperApi = new FlipperApi(flipper, entity, this);
			_tableApi.Flippers[flipper.Name] = flipperApi;
			_flippers[entity.Index] = flipperApi;
			if (EngineProvider<IDebugUI>.Exists) {
				EngineProvider<IDebugUI>.Get().OnRegisterFlipper(entity, flipper.Name);
			}

			// World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<FlipperSystem>().OnRotated +=
			// 	(sender, e) => flipperApi.HandleEvent(e);
		}

		public void RegisterKicker(Kicker kicker, Entity entity, GameObject go)
		{
			var kickerApi = new KickerApi(kicker, entity, this);
			_tableApi.Kickers[kicker.Name] = kickerApi;
		}

		public void RegisterPlunger(Plunger plunger, Entity entity, GameObject go)
		{
			_tableApi.Plungers[plunger.Name] = new PlungerApi(plunger, entity, this);
		}

		public void RegisterSurface(Surface item, Entity entity, GameObject go)
		{
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
			_player = gameObject.GetComponent<Player>();
			#if FLIPPER_LOG
			DebugLog = File.CreateText("flipper.log");
			#endif
		}

		private void Start()
		{
			// bootstrap table script(s)
			var tableScripts = GetComponents<VisualPinballScript>();
			foreach (var tableScript in tableScripts) {
				tableScript.OnAwake(_tableApi);
			}

			// trigger init events now
			foreach (var i in _tableApi.Initializables) {
				i.Init();
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
				_player.CreateBall(new DebugBallCreator());
				// _player.CreateBall(new DebugBallCreator(425, 1325));
				// _player.CreateBall(new DebugBallCreator(390, 1125));

				// _player.CreateBall(new DebugBallCreator(475, 1727.5f));
				// _tableApi.Flippers["RightFlipper"].RotateToEnd();
			}

			if (Input.GetKeyUp("n")) {
				_player.CreateBall(new DebugBallCreator(129f, 1450f));
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

		#if FLIPPER_LOG
		private void OnDestroy()
		{
			DebugLog.Dispose();
		}
		#endif

	}

	public class DebugBallCreator : IBallCreationPosition
	{
		private float _x;
		private float _y;

		private readonly float _kickAngle;
		private readonly float _kickForce;

		public DebugBallCreator()
		{
			_x = -1;
			_y = -1;
		}

		public DebugBallCreator(float x, float y)
		{
			_x = x;
			_y = y;
		}

		public DebugBallCreator(float x, float y, float kickAngle, float kickForce)
		{
			_x = x;
			_y = y;
			_kickAngle = kickAngle;
			_kickForce = kickForce;
		}

		public Vertex3D GetBallCreationPosition(Table table)
		{
			if (_x < 0 || _y < 0) {
				_x = Random.Range(table.Width / 6f, table.Width / 6f * 5f);
				_y = Random.Range(table.Height / 8f, table.Height / 2f);
			}
			return new Vertex3D(_x, _y, 0);
		}

		public Vertex3D GetBallCreationVelocity(Table table)
		{
			return new Vertex3D(
				MathF.Sin(_kickAngle) * _kickForce,
				-MathF.Cos(_kickAngle) * _kickForce,
				0
			);
		}

		public void OnBallCreated(PlayerPhysics physics, Ball ball)
		{
			// nothing to do
		}
	}
}
