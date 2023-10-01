﻿// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Unity;

namespace VisualPinballUnity
{
	/// <summary>
	/// The main simulation loop
	/// </summary>
	[DisableAutoCreation]
	internal partial class SimulateCycleSystemGroup : ComponentSystemGroup
	{
		/// <summary>
		/// Time of the next collision; other systems can update this.
		/// </summary>
		public float HitTime;

		/// <summary>
		/// Ball-ball collision resolution order is swapped each time
		/// </summary>
		public bool SwapBallCollisionHandling;

		public NativeHashMap<Entity, bool> ItemsColliding;

		public DefaultPhysicsEngine PhysicsEngine;

		public override IReadOnlyList<ComponentSystemBase> Systems => _systemsToUpdate;
		public NativeList<ContactBufferElement> Contacts;

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

		private readonly Stopwatch _simulationTime = new Stopwatch();
		private VisualPinballSimulationSystemGroup _simulationSystemGroup;

		protected override void OnStartRunning()
		{
			base.OnStartRunning();
			QuadTreeCreator.Create(EntityManager, out ItemsColliding);
		}

		protected override void OnCreate()
		{
			_flipperDataQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<FlipperMovementData>(), ComponentType.ReadOnly<FlipperStaticData>());
			_collisionEventDataQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CollisionEventData>());

			// todo re-enable systems
			// _simulationSystemGroup = World.GetExistingSystemManaged<VisualPinballSimulationSystemGroup>();
			// _staticBroadPhaseSystem = World.GetExistingSystemManaged<StaticBroadPhaseSystem>();
			// _dynamicBroadPhaseSystem = World.GetExistingSystemManaged<DynamicBroadPhaseSystem>();
			// _staticNarrowPhaseSystem = World.GetExistingSystemManaged<StaticNarrowPhaseSystem>();
			// _dynamicNarrowPhaseSystem = World.GetExistingSystemManaged<DynamicNarrowPhaseSystem>();
			// _displacementSystemGroup = World.GetExistingSystemManaged<UpdateDisplacementSystemGroup>();
			// _staticCollisionSystem = World.GetExistingSystemManaged<StaticCollisionSystem>();
			// _dynamicCollisionSystem = World.GetExistingSystemManaged<DynamicCollisionSystem>();
			// _contactSystem = World.GetExistingSystemManaged<ContactSystem>();
			// _ballSpinHackSystem = World.GetExistingSystemManaged<BallSpinHackSystem>();
			// _systemsToUpdate.Add(_staticBroadPhaseSystem);
			// _systemsToUpdate.Add(_dynamicBroadPhaseSystem);
			// _systemsToUpdate.Add(_staticNarrowPhaseSystem);
			// _systemsToUpdate.Add(_dynamicNarrowPhaseSystem);
			// _systemsToUpdate.Add(_displacementSystemGroup);
			// _systemsToUpdate.Add(_staticCollisionSystem);
			// _systemsToUpdate.Add(_dynamicCollisionSystem);
			// _systemsToUpdate.Add(_contactSystem);
			// _systemsToUpdate.Add(_ballSpinHackSystem);

			Contacts = new NativeList<ContactBufferElement>(Allocator.Persistent);
		}

		protected override void OnDestroy()
		{
			// Contacts.Dispose();
			// ItemsColliding.Dispose();
		}

		protected override void OnUpdate()
		{
			if (Application.isEditor) {
				UpdateCycle();

			} else {
																																																																	#if DEV_MODE_ENABLED
				UpdateCycle();
																																																																	#endif
			}
		}

		private void UpdateCycle()
		{
			_simulationTime.Restart();

			_staticCounts = PhysicsConstants.StaticCnts;
			var dTime = _simulationSystemGroup.PhysicsDiffTime;
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
				EngineProvider<IDebugUI>.Get().OnPhysicsUpdate(_simulationSystemGroup.CurrentPhysicsTime, numSteps, (float)_simulationTime.Elapsed.TotalMilliseconds);
			}
		}

		private void ClearContacts()
		{
			Contacts.Clear();
		}

		private void ApplyFlipperTime()
		{
			// for each flipper
			var entities = _flipperDataQuery.ToEntityArray(Allocator.TempJob);
			foreach (var entity in entities) {
				var movementData = EntityManager.GetComponentData<FlipperMovementData>(entity);
				var staticData = EntityManager.GetComponentData<FlipperStaticData>(entity);
				var tricksData = EntityManager.GetComponentData<FlipperTricksData>(entity);
				var flipperHitTime = movementData.GetHitTime(staticData.AngleStart, tricksData.AngleEnd);

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
