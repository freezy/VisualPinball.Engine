using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.VPT.Ball;
using VisualPinball.Unity.VPT.Flipper;
using VisualPinball.Unity.VPT.Kicker;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Game
{
	public class Player : MonoBehaviour
	{
		private readonly TableApi _tableApi = new TableApi();

		private readonly Dictionary<int, FlipperApi> _flippers = new Dictionary<int, FlipperApi>();
		private FlipperApi Flipper(int entityIndex) => _flippers.Values.FirstOrDefault(f => f.Entity.Index == entityIndex);

		//public static StreamWriter DebugLog;

		private Table _table;
		private EntityManager _manager;
		private BallManager _ballManager;
		public Matrix4x4 TableToWorld;

		public void RegisterFlipper(Flipper flipper, Entity entity, GameObject go)
		{
			//AttachToRoot(entity, go);
			var flipperApi = new FlipperApi(flipper, entity, this);
			_tableApi.Flippers[flipper.Name] = flipperApi;
			_flippers[entity.Index] = flipperApi;
			World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<FlipperVelocitySystem>().OnRotated +=
				(sender, e) => flipperApi.HandleEvent(e);
		}

		public void RegisterKicker(Kicker kicker, Entity entity, GameObject go)
		{
			//AttachToRoot(entity, go);
			var kickerApi = new KickerApi(kicker, entity, this);
			_tableApi.Kickers[kicker.Name] = kickerApi;
		}

		public void RegisterSurface(Surface item, Entity entity, GameObject go)
		{
			//AttachToRoot(entity, go);
		}

		public BallApi CreateBall(IBallCreationPosition ballCreator, float radius = 25, float mass = 1)
		{
			var ballApi = _ballManager.CreateBall(this, ballCreator, radius, mass);

			// var data = new BallData(radius, mass, _table.Data.DefaultBulbIntensityScaleOnBall);
			// const ballId = Ball.idCounter++;
			// const state = BallState.claim(`Ball${ballId}`, ballCreator.getBallCreationPosition(this.table));
			// state.pos.z += data.radius;
			//
			// const ball = new Ball(ballId, data, state, ballCreator.getBallCreationVelocity(this.table), player, this.table);
			//
			// ballCreator.onBallCreated(this, ball);
			//
			// this.balls.push(ball);
			// this.movers.push(ball.getMover()); // balls are always added separately to this list!
			//
			// this.hitObjectsDynamic.push(ball.hit);
			// this.hitOcTreeDynamic.fillFromVector(this.hitObjectsDynamic);
			//
			// this.currentStates[ball.getName()] = ball.getState();
			// this.previousStates[ball.getName()] = ball.getState().clone();
			// this.emit('ballCreated', ball);
			// return ball;
			return ballApi;
		}

		private void Awake()
		{
			_manager = World.DefaultGameObjectInjectionWorld.EntityManager;
			TableToWorld = transform.localToWorldMatrix;

			var tableComponent = gameObject.GetComponent<TableBehavior>();
			_table = tableComponent.CreateTable();
			_ballManager = new BallManager(_table);

			//DebugLog = File.CreateText("flipper.log");
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
		}

		private Entity GetRootEntity()
		{
			var archetype = _manager.CreateArchetype(
				typeof(LocalToWorld),
				typeof(Translation),
				typeof(Rotation),
				typeof(Scale)
			);
			var entity = _manager.CreateEntity(archetype);

			var t = transform;
			_manager.SetComponentData(entity, new Translation { Value = t.localPosition });
			_manager.SetComponentData(entity, new Rotation { Value = t.localRotation });
			_manager.AddComponentData(entity, new NonUniformScale { Value = t.localScale });

			return entity;
		}

		// private void AttachToRoot(Entity entity, GameObject go)
		// {
		// 	_manager.AddComponentData(entity, new Parent {Value = _rootEntity});
		// 	_manager.AddComponentData(entity, new LocalToParent());
		//
		// 	// now it's attached to the parent, reset local transformation
		// 	// see https://forum.unity.com/threads/adding-localtoparent-resets-child-rotation.783239/#post-5218394
		// 	_manager.AddComponentData(entity, new Translation { Value = go.transform.localPosition });
		// 	_manager.AddComponentData(entity, new Rotation { Value = go.transform.localRotation });
		// 	_manager.AddComponentData(entity, new NonUniformScale { Value = Vector3.one });
		// }

		private void OnDestroy()
		{
			//DebugLog.Dispose();
		}
	}
}
