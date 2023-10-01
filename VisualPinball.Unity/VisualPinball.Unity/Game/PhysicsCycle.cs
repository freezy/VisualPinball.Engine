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

using System;
using NativeTrees;
using Unity.Collections;
using Unity.Entities;
using VisualPinball.Engine.Common;
using VisualPinballUnity;

namespace VisualPinball.Unity
{
	public struct PhysicsCycle : IDisposable
	{
		private NativeList<ContactBufferElement> _contacts;

		[NativeDisableParallelForRestriction]
		private NativeList<int> _overlappingColliders;

		public PhysicsCycle(Allocator a)
		{
			_contacts = new NativeList<ContactBufferElement>(a);
			_overlappingColliders = new NativeList<int>(a);
		}

		internal void Simulate(float dTime, ref PhysicsState state, in NativeOctree<int> octree,
			ref BlobAssetReference<ColliderBlob> colliders, ref NativeList<BallData> balls, ref NativeHashMap<int, FlipperState> flipperStates,
			ref InsideOfs insideOfs, ref NativeQueue<EventData>.ParallelWriter events, uint timeMs)
		{

			var staticCounts = PhysicsConstants.StaticCnts;
			
			while (dTime > 0) {
				
				var hitTime = dTime;       // begin time search from now ...  until delta ends
				
				// todo apply flipper time
				
				// clear contacts
				_contacts.Clear();

				// todo dynamic broad phase

				for (var i = 0; i < balls.Length; i++) {
					var ball = balls[i];
					
					if (ball.IsFrozen) {
						continue;
					}
					
					// static broad phase
					PhysicsStaticBroadPhase.FindOverlaps(in octree, in ball, ref _overlappingColliders);
					
					// static narrow phase
					PhysicsStaticNarrowPhase.FindNextCollision(hitTime, ref ball, in _overlappingColliders, ref colliders,
						ref insideOfs, ref _contacts, ref flipperStates);

					// write ball back
					balls[i] = ball;
					
					// todo dynamic narrow phase

					// apply static time
					ApplyStaticTime(ref hitTime, ref staticCounts, in ball);
				}

				// displacement
				for (var i = 0; i < balls.Length; i++) { // todo loop through all "movers", not just balls
					var ball = balls[i];
					BallDisplacementPhysics.UpdateDisplacements(ref ball, hitTime); // use static method instead of member
					balls[i] = ball;
				}

				foreach (var i in flipperStates.GetKeyArray(Allocator.Temp)) {
					var flipperState = flipperStates[i];
					FlipperDisplacementPhysics.UpdateDisplacement(flipperState.ItemId, ref flipperState.Movement, ref flipperState.Tricks, in flipperState.Static, hitTime, ref events);
					flipperStates[i] = flipperState;
				}

				for (var i = 0; i < balls.Length; i++) {
					var ball = balls[i];
					
					// todo dynamic collision
				
					// static collision
					PhysicsStaticCollision.Collide(hitTime, ref ball, ref colliders, ref flipperStates, ref state.Random, ref events, timeMs);
					
					balls[i] = ball;
				}
				
				// handle contacts
				var b = balls[0];
				foreach (var contact in _contacts) {
					BallCollider.HandleStaticContact(ref b, in contact.CollEvent, colliders.GetFriction(contact.CollEvent.ColliderId), hitTime, state.Gravity);
				}
				balls[0] = b;

				// clear contacts
				_contacts.Clear();

				// todo ball spin hack
				
				dTime -= hitTime;  
			}
		}
		
		private static void ApplyStaticTime(ref float hitTime, ref float staticCounts, in BallData ball)
		{
			// for each collision event
			var collEvent = ball.CollisionEvent;
			if (collEvent.HasCollider() && collEvent.HitTime <= hitTime) {       // smaller hit time??
				hitTime = collEvent.HitTime;                                     // record actual event time
				if (collEvent.HitTime < PhysicsConstants.StaticTime) {           // less than static time interval
					if (--staticCounts < 0) {
						staticCounts = 0;                                       // keep from wrapping
						hitTime = PhysicsConstants.StaticTime;
					}
				}
			}
		}

		public void Dispose()
		{
			_contacts.Dispose();
			_overlappingColliders.Dispose();
		}
	}
}
