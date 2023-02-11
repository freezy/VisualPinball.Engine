// Visual Pinball Engine
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

// ReSharper disable ClassNeverInstantiated.Global

using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using VisualPinball.Unity;
using VisualPinball.Unity.VisualPinball.Unity.Game;
using Collider = UnityEngine.Collider;

namespace VisualPinballUnity
{
	[DisableAutoCreation]
	internal partial class ContactSystem : SystemBaseStub
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;

		private EntityQuery _collDataEntityQuery;
		private float3 _gravity;

		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("ContactSystem");

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystemManaged<SimulateCycleSystemGroup>();
			_collDataEntityQuery = EntityManager.CreateEntityQuery(typeof(ColliderData));
		}

		protected override void OnStartRunning()
		{
			_gravity = Object.FindObjectOfType<Player>().Gravity;
		}

		protected override void OnUpdate()
		{
			var hitTime = _simulateCycleSystemGroup.HitTime;
			var gravity = _gravity;

			// retrieve reference to static collider data
			var collEntity = _collDataEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<ColliderData>(collEntity);
			var contacts = _simulateCycleSystemGroup.Contacts;
			var ballsLookup = GetComponentLookup<BallData>();

			var marker = PerfMarker;

			Job
				.WithName("ContactJob")
				.WithCode(() =>
			{

				marker.Begin();

				ref var colliders = ref collData.Value.Value.Colliders;

				//if (rnd.NextBool()) { // swap order of contact handling randomly
				for (var i = 0; i < contacts.Length; i++) {

					var contact = contacts[i];
					ref var collEvent = ref contact.CollEvent;
					var ball = ballsLookup[Entity.Null]; // fixme jobs ballsLookup[contact.BallId];

					if (collEvent.ColliderId > -1) { // collide with static collider
						ref var coll = ref colliders[collEvent.ColliderId].Value;
						unsafe {
							fixed (VisualPinball.Unity.Collider* collider = &coll) {

								// flipper contact updates movement data
								if (coll.Type == ColliderType.Flipper) {

									var flipperMovementData = GetComponent<FlipperMovementData>(coll.ItemId);
									var flipperMaterialData = GetComponent<FlipperStaticData>(coll.ItemId);
									var flipperVelocityData = GetComponent<FlipperVelocityData>(coll.ItemId);
									((FlipperCollider*) collider)->Contact(
										ref ball, ref flipperMovementData, in collEvent,
										in flipperMaterialData, in flipperVelocityData, hitTime, in gravity);
									SetComponent(coll.ItemId, flipperMovementData);

								} else {
									VisualPinball.Unity.Collider.Contact(ref coll, ref ball, in collEvent, hitTime, in gravity);
								}
							}
						}

					} else if (collEvent.ColliderEntity != Entity.Null) { // collide with ball
						// todo move ball friction into some data component
						BallCollider.HandleStaticContact(ref ball, in collEvent, 0.3f, hitTime, in gravity);
					}

					ballsLookup[Entity.Null] = ball; // fixme jobs [contact.BallId] = ball;
				}

				marker.End();

			}).Run();
		}
	}
}
