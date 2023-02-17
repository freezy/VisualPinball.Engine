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
using VisualPinball.Engine.Common;
using VisualPinballUnity;

namespace VisualPinball.Unity
{
	public struct PhysicsCycle : IDisposable
	{
		private NativeList<ContactBufferElement> _contacts;
		private NativeList<PlaneCollider> _overlappingColliders;

		public PhysicsCycle(Allocator a)
		{
			_contacts = new NativeList<ContactBufferElement>(a);
			_overlappingColliders = new NativeList<PlaneCollider>(a);
		}

		internal void Simulate(float dTime, ref PhysicsState state, ref NativeOctree<PlaneCollider> octree, ref NativeList<BallData> balls)
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
					PhysicsStaticNarrowPhase.FindNextCollision(hitTime, ref ball, _overlappingColliders, ref _contacts);

					// write ball back
					balls[i] = ball;
					
					// todo dynamic narrow phase

					// apply static time
					ApplyStaticTime(ref hitTime, ref staticCounts, in ball);

				}

				for (var i = 0; i < balls.Length; i++) { // todo loop through all "movers", not just balls
					var ball = balls[i];
					ball.UpdateDisplacements(hitTime);
					balls[i] = ball;
				}

				// todo displacement
				
				// todo dynamic collision
				
				// todo static collision
				
				// todo handle contacts

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
