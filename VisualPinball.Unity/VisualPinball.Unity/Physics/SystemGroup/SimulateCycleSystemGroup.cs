﻿using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.VPT.Flipper;

namespace VisualPinball.Unity.Physics.SystemGroup
{
	/// <summary>
	/// The main simulation loop
	/// </summary>
	[DisableAutoCreation]
	public class SimulateCycleSystemGroup : ComponentSystemGroup
	{
		/// <summary>
		/// Time of the next collision; other systems can update this.
		/// </summary>
		public float HitTime;

		/// <summary>
		/// Ball-ball collision resolution order is swapped each time
		/// </summary>
		public bool SwapBallCollisionHandling;

		public override IEnumerable<ComponentSystemBase> Systems => _systemsToUpdate;

		private readonly List<ComponentSystemBase> _systemsToUpdate = new List<ComponentSystemBase>();

		private StaticBroadPhaseSystem _staticBroadPhaseSystem;
		private DynamicBroadPhaseSystem _dynamicBroadPhaseSystem;
		private StaticNarrowPhaseSystem _staticNarrowPhaseSystem;
		private DynamicNarrowPhaseSystem _dynamicNarrowPhaseSystem;
		private UpdateDisplacementSystemGroup _displacementSystemGroup;
		private StaticCollisionSystem _staticCollisionSystem;
		private DynamicCollisionSystem _dynamicCollisionSystem;
		private ContactSystem _contactSystem;

		private float _staticCounts;
		private EntityQuery _flipperDataQuery;
		private EntityQuery _collisionDataQuery;

		protected override void OnCreate()
		{

			_flipperDataQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<FlipperMovementData>(), ComponentType.ReadOnly<FlipperStaticData>());
			_collisionDataQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CollisionEventData>());

			_staticBroadPhaseSystem = World.GetOrCreateSystem<StaticBroadPhaseSystem>();
			_dynamicBroadPhaseSystem = World.GetOrCreateSystem<DynamicBroadPhaseSystem>();
			_staticNarrowPhaseSystem = World.GetOrCreateSystem<StaticNarrowPhaseSystem>();
			_dynamicNarrowPhaseSystem = World.GetOrCreateSystem<DynamicNarrowPhaseSystem>();
			_displacementSystemGroup = World.GetOrCreateSystem<UpdateDisplacementSystemGroup>();
			_staticCollisionSystem = World.GetOrCreateSystem<StaticCollisionSystem>();
			_dynamicCollisionSystem = World.GetOrCreateSystem<DynamicCollisionSystem>();
			_contactSystem = World.GetOrCreateSystem<ContactSystem>();
			_systemsToUpdate.Add(_staticBroadPhaseSystem);
			_systemsToUpdate.Add(_dynamicBroadPhaseSystem);
			_systemsToUpdate.Add(_staticNarrowPhaseSystem);
			_systemsToUpdate.Add(_dynamicNarrowPhaseSystem);
			_systemsToUpdate.Add(_displacementSystemGroup);
			_systemsToUpdate.Add(_staticCollisionSystem);
			_systemsToUpdate.Add(_dynamicCollisionSystem);
			_systemsToUpdate.Add(_contactSystem);
		}

		protected override void OnUpdate()
		{
			var sim = World.GetExistingSystem<VisualPinballSimulationSystemGroup>();

			_staticCounts = PhysicsConstants.StaticCnts;
			var dTime = sim.PhysicsDiffTime;
			while (dTime > 0) {

				HitTime = (float)dTime;

				ApplyFlipperTime();

				_dynamicBroadPhaseSystem.Update();
				_staticBroadPhaseSystem.Update();
				_staticNarrowPhaseSystem.Update();
				_dynamicNarrowPhaseSystem.Update();

				ApplyStaticTime();

				_displacementSystemGroup.Update();
				_dynamicCollisionSystem.Update();
				_staticCollisionSystem.Update();
				_contactSystem.Update();

				dTime -= HitTime;

				SwapBallCollisionHandling = !SwapBallCollisionHandling;
			}
		}

		private void ApplyFlipperTime()
		{
			// for each flipper
			var entities = _flipperDataQuery.ToEntityArray(Allocator.TempJob);
			foreach (var entity in entities) {
				var movementData = EntityManager.GetComponentData<FlipperMovementData>(entity);
				var staticData = EntityManager.GetComponentData<FlipperStaticData>(entity);
				var flipperHitTime = movementData.GetHitTime(staticData.AngleStart, staticData.AngleEnd);

				// if flipper comes to a rest before the end of the cycle, advance to that time
				if (flipperHitTime > 0 && flipperHitTime < HitTime) { //!! >= 0.f causes infinite loop
					HitTime = flipperHitTime;
				}
			}
			entities.Dispose();
		}

		private void ApplyStaticTime()
		{
			// for each collision event
			var entities = _collisionDataQuery.ToEntityArray(Allocator.TempJob);
			foreach (var entity in entities) {
				var collEvent = EntityManager.GetComponentData<CollisionEventData>(entity);
				if (collEvent.HasCollider() && collEvent.HitTime <= HitTime) {       // smaller hit time??
					HitTime = collEvent.HitTime;                                     // record actual event time
					if (collEvent.HitTime < PhysicsConstants.StaticTime) {           // less than static time interval
						if (--_staticCounts < 0) {
							_staticCounts = 0;                                       // keep from wrapping
							HitTime = PhysicsConstants.StaticTime;
						}
					}
				}
			}
			entities.Dispose();
		}
	}
}
