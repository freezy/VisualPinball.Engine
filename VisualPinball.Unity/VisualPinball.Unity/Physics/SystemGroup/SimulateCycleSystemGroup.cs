﻿// Visual Pinball Engine
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

using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using VisualPinball.Engine.Common;
using Debug = UnityEngine.Debug;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The main simulation loop
	/// </summary>
	[DisableAutoCreation]
	internal class SimulateCycleSystemGroup : ComponentSystemGroup
	{
		/// <summary>
		/// Time of the next collision; other systems can update this.
		/// </summary>
		public float HitTime;

		/// <summary>
		/// Ball-ball collision resolution order is swapped each time
		/// </summary>
		public bool SwapBallCollisionHandling;

		public DefaultPhysicsEngine PhysicsEngine;

		public override IEnumerable<ComponentSystemBase> Systems => _systemsToUpdate;
		public NativeList<ContactBufferElement> Contacts;
		public JobHandle ContactsDependencies;

		private readonly List<ComponentSystemBase> _systemsToUpdate = new List<ComponentSystemBase>();

		private StaticBroadPhaseSystem _staticBroadPhaseSystem;
		private DynamicBroadPhaseSystem _dynamicBroadPhaseSystem;
		private StaticNarrowPhaseSystem _staticNarrowPhaseSystem;
		private DynamicNarrowPhaseSystem _dynamicNarrowPhaseSystem;
		private UpdateDisplacementSystemGroup _displacementSystemGroup;
		private StaticCollisionSystem _staticCollisionSystem;
		private DynamicCollisionSystem _dynamicCollisionSystem;
		private ContactSystem _contactSystem;
		private BallSpinHackSystem _ballSpinHackSystem;

		private float _staticCounts;
		private EntityQuery _flipperDataQuery;
		private EntityQuery _collisionEventDataQuery;

		private Stopwatch _simulationTime = new Stopwatch();

		protected override void OnCreate()
		{
			_flipperDataQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<FlipperMovementData>(), ComponentType.ReadOnly<FlipperStaticData>());
			_collisionEventDataQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CollisionEventData>());

			_staticBroadPhaseSystem = World.GetOrCreateSystem<StaticBroadPhaseSystem>();
			_dynamicBroadPhaseSystem = World.GetOrCreateSystem<DynamicBroadPhaseSystem>();
			_staticNarrowPhaseSystem = World.GetOrCreateSystem<StaticNarrowPhaseSystem>();
			_dynamicNarrowPhaseSystem = World.GetOrCreateSystem<DynamicNarrowPhaseSystem>();
			_displacementSystemGroup = World.GetOrCreateSystem<UpdateDisplacementSystemGroup>();
			_staticCollisionSystem = World.GetOrCreateSystem<StaticCollisionSystem>();
			_dynamicCollisionSystem = World.GetOrCreateSystem<DynamicCollisionSystem>();
			_contactSystem = World.GetOrCreateSystem<ContactSystem>();
			_ballSpinHackSystem = World.GetOrCreateSystem<BallSpinHackSystem>();
			_systemsToUpdate.Add(_staticBroadPhaseSystem);
			_systemsToUpdate.Add(_dynamicBroadPhaseSystem);
			_systemsToUpdate.Add(_staticNarrowPhaseSystem);
			_systemsToUpdate.Add(_dynamicNarrowPhaseSystem);
			_systemsToUpdate.Add(_displacementSystemGroup);
			_systemsToUpdate.Add(_staticCollisionSystem);
			_systemsToUpdate.Add(_dynamicCollisionSystem);
			_systemsToUpdate.Add(_contactSystem);
			_systemsToUpdate.Add(_ballSpinHackSystem);

			Contacts = new NativeList<ContactBufferElement>(Allocator.Persistent);
		}

		protected override void OnStartRunning()
		{
			QuadTreeCreationSystem.Create(EntityManager);
		}

		protected override void OnDestroy()
		{
			Contacts.Dispose();
		}

		protected override void OnUpdate()
		{
			_simulationTime.Restart();

			var sim = World.GetExistingSystem<VisualPinballSimulationSystemGroup>();

			_staticCounts = PhysicsConstants.StaticCnts;
			var dTime = sim.PhysicsDiffTime;
			var numSteps = 0;
			while (dTime > 0) {

				HitTime = (float)dTime;

				ApplyFlipperTime();
				ClearContacts();

				_dynamicBroadPhaseSystem.Update();
				_staticBroadPhaseSystem.Update();
				_staticNarrowPhaseSystem.Update();
				_dynamicNarrowPhaseSystem.Update();

				ApplyStaticTime();

				_displacementSystemGroup.Update();
				_dynamicCollisionSystem.Update();
				_staticCollisionSystem.Update();
				_contactSystem.Update();

				ClearContacts();

				_ballSpinHackSystem.Update();

				dTime -= HitTime;

				SwapBallCollisionHandling = !SwapBallCollisionHandling;
				++numSteps;
			}

			// debug ui update
			if (EngineProvider<IDebugUI>.Exists) {
				PhysicsEngine.UpdateDebugFlipperStates();
				PhysicsEngine.PushPendingCreateBallNotifications();
				EngineProvider<IDebugUI>.Get().OnPhysicsUpdate(sim.CurrentPhysicsTime, numSteps, (float)_simulationTime.Elapsed.TotalMilliseconds);
			}
		}

		private void ClearContacts()
		{
			// if (contacts.Length > 0) {
			// 	Debug.Break();
			// }

			Contacts.Clear();;
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
			var entities = _collisionEventDataQuery.ToEntityArray(Allocator.TempJob);
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
