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
using Unity.Profiling;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Collections;
using VisualPinballUnity;

namespace VisualPinball.Unity
{
	public struct PhysicsCycle : IDisposable
	{
		private NativeList<ContactBufferElement> _contacts;

		private static readonly ProfilerMarker PerfMarker = new("PhysicsCycle");
		private static readonly ProfilerMarker PerfMarkerDisplacement = new("Displacement");
		private static readonly ProfilerMarker PerfMarkerCollision = new("Collision");
		private static readonly ProfilerMarker PerfMarkerContacts = new("Contacts");

		public PhysicsCycle(Allocator a)
		{
			_contacts = new NativeList<ContactBufferElement>(a);
		}

		internal void Simulate(ref PhysicsState state, in AABB playfieldBounds, ref NativeParallelHashSet<int> overlappingColliders, float dTime)
		{
			PerfMarker.Begin();
			var staticCounts = PhysicsConstants.StaticCnts;

			while (dTime > 0) {

				var hitTime = dTime;       // begin time search from now ...  until delta ends

				ApplyFlipperTime(ref hitTime, ref state);

				// clear contacts
				_contacts.Clear();

				// create octree of ball-to-ball collision
				var ballOctree = PhysicsDynamicBroadPhase.CreateOctree(ref state.Balls, in playfieldBounds);

				using (var enumerator = state.Balls.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var ball = ref enumerator.Current.Value;

						if (ball.IsFrozen) {
							continue;
						}

						// hit testing
						PhysicsStaticBroadPhase.FindOverlaps(in state.Octree, in ball, ref overlappingColliders);
						PhysicsStaticNarrowPhase.FindNextCollision(hitTime, ref ball, ref overlappingColliders, ref _contacts, ref state);
						PhysicsDynamicBroadPhase.FindOverlaps(in ballOctree, in ball, ref overlappingColliders, ref state.Balls);
						PhysicsDynamicNarrowPhase.FindNextCollision(ref ball, ref overlappingColliders, ref _contacts, ref state);

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
				// plunger
				using (var enumerator = state.PlungerStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var plungerState = ref enumerator.Current.Value;
						PlungerDisplacementPhysics.UpdateDisplacement(plungerState.ItemId, ref plungerState.Movement, ref plungerState.Collider,
							in plungerState.Static, hitTime, ref state.EventQueue);
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

						// dynamic collision
						PhysicsDynamicCollision.Collide(hitTime, ref ball, ref state);

						// static collision
						PhysicsStaticCollision.Collide(hitTime, ref ball, ref state);
					}
				}
				PerfMarkerCollision.End();

				// handle contacts
				PerfMarkerContacts.Begin();
				for (var i = 0; i < _contacts.Length; i++) {
					ref var contact = ref _contacts.GetElementAsRef(i);
					ref var ball = ref state.Balls.GetValueByRef(contact.BallId);
					ContactPhysics.Update(ref contact, ref ball, ref state, hitTime);
				}
				PerfMarkerContacts.End();

				// clear contacts
				_contacts.Clear();

				using (var enumerator = state.Balls.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var ball = ref enumerator.Current.Value;
						BallSpinHackPhysics.Update(ref ball);
					}
				}

				dTime -= hitTime;

				state.SwapBallCollisionHandling = !state.SwapBallCollisionHandling;
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

		private void ApplyFlipperTime(ref float hitTime, ref PhysicsState state)
		{
			// for each flipper
			using (var enumerator = state.FlipperStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var flipperState = ref enumerator.Current.Value;
					var flipperHitTime = flipperState.Movement.GetHitTime(flipperState.Static.AngleStart, flipperState.Tricks.AngleEnd);

					// if flipper comes to a rest before the end of the cycle, advance to that time
					if (flipperHitTime > 0 && flipperHitTime < hitTime) { //!! >= 0.f causes infinite loop
						hitTime = flipperHitTime;
					}
				}
			}
		}

		public void Dispose()
		{
			_contacts.Dispose();
		}
	}
}
