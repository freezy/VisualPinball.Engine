using System.Collections.Generic;
using NLog;
using Unity.Collections;
using Unity.Entities;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Physics.Collision;

namespace VisualPinball.Unity.Physics.SystemGroup
{
	[DisableAutoCreation]
	public class SimulateCycleSystemGroup : ComponentSystemGroup
	{
		public float HitTime;

		public override IEnumerable<ComponentSystemBase> Systems => _systemsToUpdate;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly List<ComponentSystemBase> _systemsToUpdate = new List<ComponentSystemBase>();
		private BallBroadPhaseSystem _ballBroadPhaseSystem;
		private BallDynamicBroadPhaseSystem _ballDynamicBroadPhaseSystem;
		private BallNarrowPhaseSystemGroup _ballNarrowPhaseSystemGroup;
		private UpdateDisplacementSystemGroup _displacementSystemGroup;
		private BallResolveCollisionSystem _ballResolveCollisionSystem;
		private BallContactSystem _ballContactSystem;

		protected override void OnCreate()
		{
			_ballBroadPhaseSystem = World.GetOrCreateSystem<BallBroadPhaseSystem>();
			_ballDynamicBroadPhaseSystem = World.GetOrCreateSystem<BallDynamicBroadPhaseSystem>();
			_ballNarrowPhaseSystemGroup = World.GetOrCreateSystem<BallNarrowPhaseSystemGroup>();
			_displacementSystemGroup = World.GetOrCreateSystem<UpdateDisplacementSystemGroup>();
			_ballResolveCollisionSystem = World.GetOrCreateSystem<BallResolveCollisionSystem>();
			_ballContactSystem = World.GetOrCreateSystem<BallContactSystem>();
			_systemsToUpdate.Add(_ballBroadPhaseSystem);
			_systemsToUpdate.Add(_ballNarrowPhaseSystemGroup);
			_systemsToUpdate.Add(_displacementSystemGroup);
			_systemsToUpdate.Add(_ballResolveCollisionSystem);
			_systemsToUpdate.Add(_ballContactSystem);
		}

		protected override void OnUpdate()
		{
			var sim = World.GetExistingSystem<VisualPinballSimulationSystemGroup>();

			var staticCnts = PhysicsConstants.StaticCnts;
			var dTime = sim.PhysicsDiffTime;
			while (dTime > 0) {

				//Logger.Info("     ({0}) Player::PhysicsSimulateCycle (loop)\n", DTime);

				HitTime = (float)dTime;

				// // find earliest time where a flipper collides with its stop
				// for (size_t i = 0; i < m_vFlippers.size(); ++i)
				// {
				// 	const float fliphit = m_vFlippers[i]->GetHitTime();
				// 	if (fliphit > 0.f && fliphit < hittime) {//!! >= 0.f causes infinite loop
				// 		fprintf(m_flog, "     flipper hit\n");
				// 		hittime = fliphit;
				// 	}
				// }

				_ballDynamicBroadPhaseSystem.Update();
				_ballBroadPhaseSystem.Update();
				_ballNarrowPhaseSystemGroup.Update();

				// update hittime
				var collDataEntityQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CollisionEventData>());
				var entities = collDataEntityQuery.ToEntityArray(Allocator.TempJob);
				foreach (var entity in entities) {
					var collEvent = EntityManager.GetComponentData<CollisionEventData>(entity);
					if (collEvent.HitTime <= HitTime) {                                  // smaller hit time??
						HitTime = collEvent.HitTime;                                     // record actual event time
						if (collEvent.HitTime < PhysicsConstants.StaticTime) {           // less than static time interval
							if (--staticCnts < 0) {
								staticCnts = 0;                                          // keep from wrapping
								HitTime = PhysicsConstants.StaticTime;
							}
						}
					}
				}
				entities.Dispose();

				_displacementSystemGroup.Update();
				_ballResolveCollisionSystem.Update();
				_ballContactSystem.Update();

				dTime -= HitTime;
			}
		}
	}
}
