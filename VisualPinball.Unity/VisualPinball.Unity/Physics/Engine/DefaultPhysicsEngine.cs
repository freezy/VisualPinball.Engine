using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity.VPT.Ball;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Physics.Engine
{
	public class DefaultPhysicsEngine : IPhysicsEngineNew
	{
		private readonly EntityManager EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

		private EntityQuery _ballQuery;

		private Matrix4x4 _worldToLocal;
		private bool _wordlToLocalSet;

		private int _pendingOnCreateBall = 0;
		private int _lastSendOnCreateBall = -1;

		private Matrix4x4 worldToLocal { get {
			if (!_wordlToLocalSet) {
				_wordlToLocalSet = true;
				var tables = GameObject.FindObjectsOfType<TableBehavior>();
				if (tables.Length > 0) {
					_worldToLocal = tables[0].gameObject.transform.worldToLocalMatrix;
				}
			}

			return _worldToLocal;
		} }

		public DefaultPhysicsEngine()
		{
			_ballQuery = EntityManager.CreateEntityQuery((ComponentType.ReadOnly<BallData>()));
		}

		public string Name => "Default VPX";

		public void OnRegisterFlipper(Entity entity, string name)
		{
			throw new System.NotImplementedException();
		}

		public void OnPhysicsUpdate(int numSteps, float processingTime)
		{
		}

		public void OnCreateBall(Entity entity, float3 position, float3 velocity, float radius, float mass)
		{
			throw new System.NotImplementedException();
		}

		public void OnRotateToEnd(Entity entity)
		{
			throw new System.NotImplementedException();
		}

		public void OnRotateToStart(Entity entity)
		{
			throw new System.NotImplementedException();
		}

		public bool UsePureEntity { get; }
	}
}
