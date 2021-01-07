// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

// ReSharper disable ClassNeverInstantiated.Global

using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace VisualPinball.Unity
{
	[DisableAutoCreation]
	internal class ContactSystem : SystemBase
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private StaticNarrowPhaseSystem _staticNarrowPhaseSystem;
		private DynamicNarrowPhaseSystem _dynamicNarrowPhaseSystem;

		private EntityQuery _collDataEntityQuery;
		private float3 _gravity;

		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("ContactSystem");

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
			_dynamicNarrowPhaseSystem = World.GetOrCreateSystem<DynamicNarrowPhaseSystem>();
			_staticNarrowPhaseSystem = World.GetOrCreateSystem<StaticNarrowPhaseSystem>();
			_collDataEntityQuery = EntityManager.CreateEntityQuery(typeof(ColliderData));
		}

		protected override void OnStartRunning()
		{
			_gravity = Object.FindObjectOfType<Player>().GetGravity();
		}

		protected override void OnUpdate()
		{
			var hitTime = _simulateCycleSystemGroup.HitTime;
			var gravity = _gravity;

			// retrieve reference to static collider data
			var collEntity = _collDataEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<ColliderData>(collEntity);
			var contacts = _simulateCycleSystemGroup.Contacts;
			var ballsLookup = GetComponentDataFromEntity<BallData>();

			var marker = PerfMarker;

			Job
				.WithName("ContactJob")
//				.WithReadOnly(contacts)
//				.WithReadOnly(ballsLookup)
				.WithCode(() =>
			{

				marker.Begin();

				ref var colliders = ref collData.Value.Value.Colliders;

				//if (rnd.NextBool()) { // swap order of contact handling randomly
				for (var i = 0; i < contacts.Length; i++) {

					var contact = contacts[i];
					ref var collEvent = ref contact.CollEvent;
					var ball = ballsLookup[contact.BallEntity];

					if (collEvent.ColliderId > -1) { // collide with static collider
						ref var coll = ref colliders[collEvent.ColliderId].Value;
						unsafe {
							fixed (Collider* collider = &coll) {

								// flipper contact updates movement data
								if (coll.Type == ColliderType.Flipper) {

									var flipperMovementData = GetComponent<FlipperMovementData>(coll.Entity);
									var flipperMaterialData = GetComponent<FlipperStaticData>(coll.Entity);
									var flipperVelocityData = GetComponent<FlipperVelocityData>(coll.Entity);
									((FlipperCollider*) collider)->Contact(
										ref ball, ref flipperMovementData, in collEvent,
										in flipperMaterialData, in flipperVelocityData, hitTime, in gravity);
									SetComponent(coll.Entity, flipperMovementData);

								} else {
									Collider.Contact(ref coll, ref ball, in collEvent, hitTime, in gravity);
								}
							}
						}

					} else if (collEvent.ColliderEntity != Entity.Null) { // collide with ball
						// todo move ball friction into some data component
						BallCollider.HandleStaticContact(ref ball, in collEvent, 0.3f, hitTime, in gravity);
					}

					ballsLookup[contact.BallEntity] = ball;
				}

				marker.End();

			}).Run();
		}
	}
}
