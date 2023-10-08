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
using Unity.Collections;
using Unity.Profiling;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Collections;
using VisualPinballUnity;

namespace VisualPinball.Unity
{
	public struct PhysicsCycle : IDisposable
	{
		private NativeList<ContactBufferElement> _contacts;

		[NativeDisableParallelForRestriction]
		private NativeList<int> _overlappingColliders;

		private static readonly ProfilerMarker PerfMarker = new("PhysicsCycle");
		private static readonly ProfilerMarker PerfMarkerBroadPhase = new("BroadPhase");
		private static readonly ProfilerMarker PerfMarkerNarrowPhase = new("NarrowPhase");
		private static readonly ProfilerMarker PerfMarkerDisplacement = new("Displacement");
		private static readonly ProfilerMarker PerfMarkerCollision = new("Collision");
		private static readonly ProfilerMarker PerfMarkerContacts = new("Contacts");

		public PhysicsCycle(Allocator a)
		{
			_contacts = new NativeList<ContactBufferElement>(a);
			_overlappingColliders = new NativeList<int>(a);
		}

		internal void Simulate(ref PhysicsState state, float dTime, uint timeMs)
		{
			PerfMarker.Begin();
			var staticCounts = PhysicsConstants.StaticCnts;
			
			while (dTime > 0) {
				
				var hitTime = dTime;       // begin time search from now ...  until delta ends
				
				// todo apply flipper time
				
				// clear contacts
				_contacts.Clear();

				// todo dynamic broad phase

				using (var enumerator = state.Balls.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var ball = ref enumerator.Current.Value;

						if (ball.IsFrozen) {
							continue;
						}

						// static broad phase
						PerfMarkerBroadPhase.Begin();
						PhysicsStaticBroadPhase.FindOverlaps(in state.Octree, in ball, ref _overlappingColliders);
						PerfMarkerBroadPhase.End();

						// static narrow phase
						PerfMarkerNarrowPhase.Begin();
						PhysicsStaticNarrowPhase.FindNextCollision(hitTime, ref ball, in _overlappingColliders, ref _contacts, ref state);
						PerfMarkerNarrowPhase.End();

						// todo dynamic narrow phase

						// apply static time
						ApplyStaticTime(ref hitTime, ref staticCounts, in ball);
					}
				}

				#region Displacement
				PerfMarkerDisplacement.Begin();

				// balls
				using (var enumerator = state.Balls.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						BallDisplacementPhysics.UpdateDisplacements(ref enumerator.Current.Value, hitTime); // use static method instead of member
					}
				}
				// flippers
				using (var enumerator = state.FlipperStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var flipperState = ref enumerator.Current.Value;
						FlipperDisplacementPhysics.UpdateDisplacement(flipperState.ItemId, ref flipperState.Movement,
							ref flipperState.Tricks, in flipperState.Static, hitTime, ref state.EventQueue);
					}
				}
				// gates
				using (var enumerator = state.GateStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var gateState = ref enumerator.Current.Value;
						GateDisplacementPhysics.UpdateDisplacement(gateState.ItemId, ref gateState.Movement, in gateState.Static,
							hitTime, ref state.EventQueue);
					}
				}
				// spinners
				using (var enumerator = state.SpinnerStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var spinnerState = ref enumerator.Current.Value;
						SpinnerDisplacementPhysics.UpdateDisplacement(spinnerState.ItemId, ref spinnerState.Movement, in spinnerState.Static,
							hitTime, ref state.EventQueue);
					}
				}

				PerfMarkerDisplacement.End();
				#endregion

				// collision
				PerfMarkerCollision.Begin();
				using (var enumerator = state.Balls.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var ball = ref enumerator.Current.Value;

						// todo dynamic collision

						// static collision
						PhysicsStaticCollision.Collide(hitTime, ref ball, timeMs, ref state);
					}
				}
				PerfMarkerCollision.End();

				// handle contacts
				PerfMarkerContacts.Begin();
				for (var i = 0; i < _contacts.Length; i++) {
					ref var contact = ref _contacts.GetElementAsRef(i);
					ref var ball = ref state.Balls.GetValueByRef(contact.BallId);
					BallCollider.HandleStaticContact(ref ball, in contact.CollEvent, state.Colliders.GetFriction(contact.CollEvent.ColliderId), hitTime, state.Env.Gravity);
				}
				PerfMarkerContacts.End();

				// clear contacts
				_contacts.Clear();

				// todo ball spin hack
				
				dTime -= hitTime;  
			}
			PerfMarker.End();
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
