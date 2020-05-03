using System.Collections.Generic;
using NLog;
using Unity.Collections;
using Unity.Entities;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Physics.Collision;

namespace VisualPinball.Unity.Physics.SystemGroup
{
	[DisableAutoCreation]
	public class SimulateCycleSystemGroup : ComponentSystemGroup
	{
		public float HitTime;
		public bool SwapBallCollisionHandling = true;

		public override IEnumerable<ComponentSystemBase> Systems => _systemsToUpdate;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly List<ComponentSystemBase> _systemsToUpdate = new List<ComponentSystemBase>();
		private StaticBroadPhaseSystem _staticBroadPhaseSystem;
		private DynamicBroadPhaseSystem _dynamicBroadPhaseSystem;
		private NarrowPhaseSystem _narrowPhaseSystem;
		private UpdateDisplacementSystemGroup _displacementSystemGroup;
		private StaticCollisionSystem _staticCollisionSystem;
		private DynamicCollisionSystem _dynamicCollisionSystem;
		private ContactSystem _contactSystem;

		protected override void OnCreate()
		{
			_staticBroadPhaseSystem = World.GetOrCreateSystem<StaticBroadPhaseSystem>();
			_dynamicBroadPhaseSystem = World.GetOrCreateSystem<DynamicBroadPhaseSystem>();
			_narrowPhaseSystem = World.GetOrCreateSystem<NarrowPhaseSystem>();
			_displacementSystemGroup = World.GetOrCreateSystem<UpdateDisplacementSystemGroup>();
			_staticCollisionSystem = World.GetOrCreateSystem<StaticCollisionSystem>();
			_dynamicCollisionSystem = World.GetOrCreateSystem<DynamicCollisionSystem>();
			_contactSystem = World.GetOrCreateSystem<ContactSystem>();
			_systemsToUpdate.Add(_staticBroadPhaseSystem);
			_systemsToUpdate.Add(_dynamicBroadPhaseSystem);
			_systemsToUpdate.Add(_narrowPhaseSystem);
			_systemsToUpdate.Add(_displacementSystemGroup);
			_systemsToUpdate.Add(_staticCollisionSystem);
			_systemsToUpdate.Add(_dynamicCollisionSystem);
			_systemsToUpdate.Add(_contactSystem);
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

				_dynamicBroadPhaseSystem.Update();
				_staticBroadPhaseSystem.Update();
				_narrowPhaseSystem.Update();

				// update hittime
				var collDataEntityQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CollisionEventData>());
				var entities = collDataEntityQuery.ToEntityArray(Allocator.TempJob);
				foreach (var entity in entities) {
					var collEvent = EntityManager.GetComponentData<CollisionEventData>(entity);
					if (collEvent.HasCollider() && collEvent.HitTime <= HitTime) {       // smaller hit time??
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
				_staticCollisionSystem.Update();
				_dynamicCollisionSystem.Update();
				_contactSystem.Update();

				dTime -= HitTime;

				SwapBallCollisionHandling = !SwapBallCollisionHandling;
			}
		}
	}
}
