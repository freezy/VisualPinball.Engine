using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.VPT.Flipper;
using VisualPinball.Unity.VPT.Table;
using BallData = VisualPinball.Unity.VPT.Ball.BallData;

namespace VisualPinball.Unity.DebugAndPhysicsComunicationProxy
{
	public class VPX_PhysicsEngineProxyClient : IPhysicsEngine
    {
        private readonly EntityManager EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
		private EntityQuery _ballQuery;
		private Matrix4x4 _worldToLocal;
		private bool _wordlToLocalSet = false;
		private Matrix4x4 worldToLocal { get {
				if (!_wordlToLocalSet) 
				{
					_wordlToLocalSet = true;
					TableBehavior[] tables = GameObject.FindObjectsOfType<TableBehavior>();
					if (tables.Length > 0)
						_worldToLocal = tables[0].gameObject.transform.worldToLocalMatrix;
				}

				return _worldToLocal;
			} }

		public VPX_PhysicsEngineProxyClient()
		{
			_ballQuery = EntityManager.CreateEntityQuery((ComponentType.ReadOnly<BallData>()));			
		}

		Dictionary<Entity, FlipperState> _flippers = new Dictionary<Entity, FlipperState>();

        public void OnRegisterFlipper(Entity entity, string name) { _flippers[entity] = new FlipperState(); }

        public void OnPhysicsUpdate(int numSteps, float processingTime)
        {
            // store state of flippers
            var keys = _flippers.Keys.ToList();
            foreach (var entity in keys)
            {
                var fmd = EntityManager.GetComponentData<FlipperMovementData>(entity);
                var fsd = EntityManager.GetComponentData<FlipperStaticData>(entity);
                var fss = EntityManager.GetComponentData<SolenoidStateData>(entity);
                _flippers[entity] = new FlipperState(
                    math.degrees(math.abs(fmd.Angle - fsd.AngleStart)),
                    fss.Value);
            }

			// search for balls with pending notification to DebugUI
			if (_pendingOnCreateBall > 0)
			{
				using (var ballEntities = _ballQuery.ToEntityArray(Allocator.TempJob))
				{
					foreach (var entity in ballEntities)
					{
						if (entity.Index < 0) // this should not happed?
							continue;

						var ballData = EntityManager.GetComponentData<BallData>(entity);
						if (ballData.Id == _lastSendOnCreateBall + 1)
						{
							++_lastSendOnCreateBall;
							--_pendingOnCreateBall;
							DPProxy.debugUI?.OnCreateBall(entity);
						}
					}
				}
			}
        }

		private int _pendingOnCreateBall = 0;
		private int _lastSendOnCreateBall = -1;
        public void OnCreateBall(Entity entity, float3 position, float3 velocity, float radius, float mass) 
		{
			if (entity.Index < 0)
				++_pendingOnCreateBall;
		}

        public void OnRotateToEnd(Entity entity) { }
        public void OnRotateToStart(Entity entity) { }
        public bool UsePureEntity() { return false; }

        public bool GetFlipperState(Entity entity, out FlipperState flipperState)
        {
            return _flippers.TryGetValue(entity, out flipperState);
        }

        public float GetFloat(Params param) { return 0; }
        public void SetFloat(Params param, float val) { }
        public void ManualBallRoller(Entity entity, float3 targetPosition) 
        {
			// fail safe, if we get invalide entity
			if (entity == Entity.Null && entity.Index != -1)
				return;

			float3 target = worldToLocal.MultiplyPoint(targetPosition);
			var ballData = EntityManager.GetComponentData<BallData>(entity);
			ballData.Velocity = float3.zero;
			ballData.AngularVelocity = float3.zero;
			ballData.AngularMomentum = float3.zero;
			ballData.IsFrozen = false;

			var dir = (target - ballData.Position);
			float dist = math.length(dir);
			if (dist > 50)
				dist = 50;
			if (dist > 0.1f)
			{
				dist = dist + 1.0f;
				ballData.Velocity = dir * dist * 0.001f;
				EntityManager.SetComponentData(entity, ballData);
			}			            
        }
    }
}
