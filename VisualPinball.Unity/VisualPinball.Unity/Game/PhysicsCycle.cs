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
using Unity.Mathematics;
using Unity.Profiling;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	public struct PhysicsCycle : IDisposable
	{
		private NativeList<ContactBufferElement> _contacts;
		private NativeList<MechanicalDropTargetContactCandidate> _mechanicalDropTargetContacts;
		private NativeList<MechanicalDropTargetContact> _mechanicalImpactContacts;

		private static readonly ProfilerMarker PerfMarker = new("PhysicsCycle");
		private static readonly ProfilerMarker PerfMarkerDisplacement = new("Displacement");
		private static readonly ProfilerMarker PerfMarkerCollision = new("Collision");
		private static readonly ProfilerMarker PerfMarkerContacts = new("Contacts");
		private static readonly ProfilerMarker PerfMarkerMechanicalTargetContacts = new("DropTarget.ContactReduction");

		public PhysicsCycle(Allocator a)
		{
			_contacts = new NativeList<ContactBufferElement>(a);
			_mechanicalDropTargetContacts = new NativeList<MechanicalDropTargetContactCandidate>(a);
			_mechanicalImpactContacts = new NativeList<MechanicalDropTargetContact>(a);
		}

		internal void Simulate(ref PhysicsState state, ref NativeParallelHashSet<int> overlappingColliders, ref NativeOctree<int> kinematicOctree, ref NativeOctree<int> ballOctree, float dTime)
		{
			PerfMarker.Begin();
			var staticCounts = PhysicsConstants.StaticCnts;

			// rebuild octree of ball-to-ball collision (clear + re-insert, no alloc)
			// it's okay to have this code outside of the inner loop, as the ball hitrects already include the maximum distance they can travel in that timespan
			PhysicsDynamicBroadPhase.RebuildOctree(ref ballOctree, ref state.Balls);

			while (dTime > 0) {

				var hitTime = dTime;       // begin time search from now ...  until delta ends

				ApplyFlipperTime(ref hitTime, ref state);

				// clear contacts
				_contacts.Clear();

				using (var enumerator = state.Balls.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var ball = ref enumerator.Current.Value;

						if (ball.IsFrozen) {
							continue;
						}

						// init contacts and event
						ball.CollisionEvent.ClearCollider(hitTime); // search upto current hit time

						// hit testing (overlappingColliders is cleared in broad phase)
						PhysicsStaticBroadPhase.FindOverlaps(in state.Octree, in ball, ref overlappingColliders);
						PhysicsStaticNarrowPhase.FindNextCollision(ref state.Colliders, ref ball, ref overlappingColliders, ref _contacts, ref state);

						PhysicsStaticBroadPhase.FindOverlaps(in kinematicOctree, in ball, ref overlappingColliders);
						PhysicsStaticNarrowPhase.FindNextCollision(ref state.KinematicColliders, ref ball, ref overlappingColliders, ref _contacts, ref state);

						// no negative time allowed
						if (ball.CollisionEvent.HitTime < 0) {
							ball.CollisionEvent.ClearCollider();
						}

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
						FlipperDisplacementPhysics.UpdateDisplacement(enumerator.Current.Key, ref flipperState.Movement,
							ref flipperState.Tricks, in flipperState.Static, hitTime, ref state.EventQueue);
					}
				}
				// gates
				using (var enumerator = state.GateStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var gateState = ref enumerator.Current.Value;
						GateDisplacementPhysics.UpdateDisplacement(enumerator.Current.Key, ref gateState.Movement, in gateState.Static,
							hitTime, ref state.EventQueue);
					}
				}
				// plunger
				using (var enumerator = state.PlungerStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var plungerState = ref enumerator.Current.Value;
						ref var plungerCollider = ref state.Colliders.Plunger(plungerState.Static.ColliderId);
						PlungerDisplacementPhysics.UpdateDisplacement(enumerator.Current.Key, ref plungerState.Movement, ref plungerCollider,
							in plungerState.Static, hitTime, ref state.EventQueue);
					}
				}
				// spinners
				using (var enumerator = state.SpinnerStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var spinnerState = ref enumerator.Current.Value;
						SpinnerDisplacementPhysics.UpdateDisplacement(enumerator.Current.Key, ref spinnerState.Movement, in spinnerState.Static,
							hitTime, ref state.EventQueue);
					}
				}

				PerfMarkerDisplacement.End();
				#endregion

				// collision
				PerfMarkerCollision.Begin();
				if (state.HasMechanicalDropTargets) {
					using (var enumerator = state.Balls.GetEnumerator()) {
						while (enumerator.MoveNext()) {
							ref var ball = ref enumerator.Current.Value;
							PhysicsDynamicCollision.Collide(hitTime, ref ball, ref state);
						}
					}
					ResolveMechanicalDropTargetContacts(hitTime, ref state);
					using (var enumerator = state.Balls.GetEnumerator()) {
						while (enumerator.MoveNext()) {
							ref var ball = ref enumerator.Current.Value;
							PhysicsStaticCollision.Collide(hitTime, ref ball, ref state);
						}
					}
				} else {
					// Preserve the original per-ball dynamic/static ordering for every table
					// that has not opted into Mechanical drop-target physics.
					using (var enumerator = state.Balls.GetEnumerator()) {
						while (enumerator.MoveNext()) {
							ref var ball = ref enumerator.Current.Value;
							PhysicsDynamicCollision.Collide(hitTime, ref ball, ref state);
							PhysicsStaticCollision.Collide(hitTime, ref ball, ref state);
						}
					}
				}
				PerfMarkerCollision.End();

				// handle contacts
				PerfMarkerContacts.Begin();
				if (state.HasMechanicalDropTargets) {
					ResolveMechanicalDropTargetSupportContacts(hitTime, ref state);
				}
				for (var i = 0; i < _contacts.Length; i++) {
					ref var contact = ref _contacts.GetElementAsRef(i);
					if (contact.Handled != 0) {
						continue;
					}
					ref var ball = ref state.Balls.GetValueByRef(contact.BallId);
					if (contact.CollEvent.IsKinematic) {
						ContactPhysics.Update(ref contact, ref ball, ref state, ref state.KinematicColliders, hitTime);
					} else {
						ContactPhysics.Update(ref contact, ref ball, ref state, ref state.Colliders, hitTime);
					}
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
		
		private static void ApplyStaticTime(ref float hitTime, ref float staticCounts, in BallState ball)
		{
			// for each collision event
			var collEvent = ball.CollisionEvent;
			if (collEvent.HasCollider() && collEvent.HitTime <= hitTime) {       // smaller hit time??
				hitTime = collEvent.HitTime;                                     // record actual event time
				if (hitTime < PhysicsConstants.StaticTime) {           // less than static time interval
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
			_mechanicalDropTargetContacts.Dispose();
			_mechanicalImpactContacts.Dispose();
		}

		private void ResolveMechanicalDropTargetContacts(float hitTime, ref PhysicsState state)
		{
			PerfMarkerMechanicalTargetContacts.Begin();
			_mechanicalDropTargetContacts.Clear();
			using (var enumerator = state.Balls.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var ball = ref enumerator.Current.Value;
					var collEvent = ball.CollisionEvent;
					if (collEvent.ColliderId < 0 || collEvent.HitTime > hitTime) {
						continue;
					}

					var header = collEvent.IsKinematic
						? state.KinematicColliders.GetHeader(collEvent.ColliderId)
						: state.Colliders.GetHeader(collEvent.ColliderId);
					if (!state.DropTargetStates.TryGetValue(header.ItemId, out var target)
						|| target.Static.PhysicsMode != DropTargetPhysicsMode.Mechanical) {
						continue;
					}
					var isTransformed = collEvent.IsKinematic
						? state.KinematicColliders.IsTransformed(collEvent.ColliderId)
						: state.Colliders.IsTransformed(collEvent.ColliderId);
					if (!isTransformed) {
						// Mechanical contact math is evaluated in playfield space. Advanced target
						// mesh colliders are transformable; retain the generic local-space path for
						// any future non-transformable representation.
						continue;
					}

					_mechanicalDropTargetContacts.Add(new MechanicalDropTargetContactCandidate(
						collEvent.HitTime, header.ItemId, enumerator.Current.Key, collEvent.ColliderId,
						header.Role, collEvent.IsKinematic));
				}
			}

			_mechanicalDropTargetContacts.AsArray().Sort();
			for (var groupStart = 0; groupStart < _mechanicalDropTargetContacts.Length;) {
				var hitTimeKey = _mechanicalDropTargetContacts[groupStart].HitTimeKey;
				var targetItemId = _mechanicalDropTargetContacts[groupStart].TargetItemId;
				var groupEnd = groupStart + 1;
				while (groupEnd < _mechanicalDropTargetContacts.Length
					&& _mechanicalDropTargetContacts[groupEnd].HitTimeKey == hitTimeKey
					&& _mechanicalDropTargetContacts[groupEnd].TargetItemId == targetItemId) {
					groupEnd++;
				}

				ref var target = ref state.DropTargetStates.GetValueByRef(targetItemId);
				var isResetStroke = target.Mechanical.State == DropTargetMechanismState.Resetting
					|| target.Mechanical.State == DropTargetMechanismState.Settling;
				_mechanicalImpactContacts.Clear();
				for (var i = groupStart; i < groupEnd; i++) {
					var candidate = _mechanicalDropTargetContacts[i];
					ref var ball = ref state.Balls.GetValueByRef(candidate.BallId);
					var collEvent = ball.CollisionEvent;
					var header = collEvent.IsKinematic
						? state.KinematicColliders.GetHeader(collEvent.ColliderId)
						: state.Colliders.GetHeader(collEvent.ColliderId);
					var hitNormal = math.normalizesafe(collEvent.HitNormal);
					var faceAlignment = math.abs(math.dot(hitNormal,
						math.normalizesafe(target.Static.FaceNormal)));
					if (header.Role != ColliderRole.DropTargetPhysicalFace
						|| target.Mechanical.State == DropTargetMechanismState.Down
						|| (!isResetStroke && faceAlignment < 0.5f)) {
						continue;
					}

					var approachSpeed = -math.dot(ball.Velocity
						- MechanicalDropTargetPhysics.SurfaceVelocityAtPoint(in target.Static,
							in target.Mechanical, ball.Position - ball.Radius * hitNormal),
						hitNormal);
					_mechanicalImpactContacts.Add(new MechanicalDropTargetContact {
						BallId = candidate.BallId,
						Ball = ball,
						Normal = hitNormal,
						Restitution = TargetCollider.ResolveElasticity(in header.Material, in collEvent,
							approachSpeed, ref state),
						Friction = TargetCollider.ResolveFriction(in header.Material, in collEvent,
							approachSpeed, ref state),
					});
				}

				MechanicalDropTargetPhysics.ResolveImpactGroup(ref _mechanicalImpactContacts,
					ref target.Mechanical, in target.Static);
				for (var i = 0; i < _mechanicalImpactContacts.Length; i++) {
					ref var contact = ref _mechanicalImpactContacts.GetElementAsRef(i);
					ref var ball = ref state.Balls.GetValueByRef(contact.BallId);
					var collEvent = ball.CollisionEvent;
					var header = collEvent.IsKinematic
						? state.KinematicColliders.GetHeader(collEvent.ColliderId)
						: state.Colliders.GetHeader(collEvent.ColliderId);
					ball = contact.Ball;
					TargetCollider.CompleteMechanicalImpact(ref ball, ref target, in collEvent, in header,
						contact.ApproachSpeed, contact.NormalImpulse, ref state.EventQueue);
					ball.CollisionEvent.ClearCollider();
				}

				for (var i = groupStart; i < groupEnd; i++) {
					var candidate = _mechanicalDropTargetContacts[i];
					ref var ball = ref state.Balls.GetValueByRef(candidate.BallId);
					if (ball.CollisionEvent.ColliderId != candidate.ColliderId
						|| ball.CollisionEvent.IsKinematic != (candidate.IsKinematic != 0)) {
						continue;
					}
					PhysicsStaticCollision.Collide(hitTime, ref ball, ref state);
					ball.CollisionEvent.ClearCollider();
				}

				groupStart = groupEnd;
			}
			PerfMarkerMechanicalTargetContacts.End();
		}

		private void ResolveMechanicalDropTargetSupportContacts(float hitTime, ref PhysicsState state)
		{
			PerfMarkerMechanicalTargetContacts.Begin();
			_mechanicalDropTargetContacts.Clear();
			for (var i = 0; i < _contacts.Length; i++) {
				ref var contact = ref _contacts.GetElementAsRef(i);
				var collEvent = contact.CollEvent;
				if (collEvent.ColliderId < 0) {
					continue;
				}
				var header = collEvent.IsKinematic
					? state.KinematicColliders.GetHeader(collEvent.ColliderId)
					: state.Colliders.GetHeader(collEvent.ColliderId);
				if (!state.DropTargetStates.TryGetValue(header.ItemId, out var target)
					|| target.Static.PhysicsMode != DropTargetPhysicsMode.Mechanical
					|| (target.Mechanical.State != DropTargetMechanismState.Resetting
						&& target.Mechanical.State != DropTargetMechanismState.Settling)) {
					continue;
				}
				var isTransformed = collEvent.IsKinematic
					? state.KinematicColliders.IsTransformed(collEvent.ColliderId)
					: state.Colliders.IsTransformed(collEvent.ColliderId);
				if (!isTransformed) {
					continue;
				}
				_mechanicalDropTargetContacts.Add(new MechanicalDropTargetContactCandidate(
					collEvent.HitTime, header.ItemId, contact.BallId, collEvent.ColliderId,
					header.Role, collEvent.IsKinematic, i));
			}

			_mechanicalDropTargetContacts.AsArray().Sort();
			for (var i = 0; i < _mechanicalDropTargetContacts.Length; i++) {
				var candidate = _mechanicalDropTargetContacts[i];
				ref var contact = ref _contacts.GetElementAsRef(candidate.ContactIndex);
				ref var ball = ref state.Balls.GetValueByRef(contact.BallId);
				if (contact.CollEvent.IsKinematic) {
					ContactPhysics.Update(ref contact, ref ball, ref state, ref state.KinematicColliders, hitTime);
				} else {
					ContactPhysics.Update(ref contact, ref ball, ref state, ref state.Colliders, hitTime);
				}
				contact.Handled = 1;
			}
			PerfMarkerMechanicalTargetContacts.End();
		}

		internal readonly struct MechanicalDropTargetContactCandidate : IComparable<MechanicalDropTargetContactCandidate>
		{
			private const float HitTimeQuantization = 1000000f;

			internal readonly int HitTimeKey;
			internal readonly int TargetItemId;
			internal readonly int BallId;
			internal readonly int ColliderId;
			internal readonly ColliderRole Role;
			internal readonly byte IsKinematic;
			internal readonly int ContactIndex;

			internal MechanicalDropTargetContactCandidate(float hitTime, int targetItemId, int ballId,
				int colliderId, ColliderRole role, bool isKinematic, int contactIndex = -1)
			{
				HitTimeKey = (int)math.round(hitTime * HitTimeQuantization);
				TargetItemId = targetItemId;
				BallId = ballId;
				ColliderId = colliderId;
				Role = role;
				IsKinematic = isKinematic ? (byte)1 : (byte)0;
				ContactIndex = contactIndex;
			}

			public int CompareTo(MechanicalDropTargetContactCandidate other)
			{
				var result = HitTimeKey.CompareTo(other.HitTimeKey);
				if (result != 0) {
					return result;
				}
				result = TargetItemId.CompareTo(other.TargetItemId);
				if (result != 0) {
					return result;
				}
				result = BallId.CompareTo(other.BallId);
				if (result != 0) {
					return result;
				}
				result = ((byte)Role).CompareTo((byte)other.Role);
				if (result != 0) {
					return result;
				}
				result = ColliderId.CompareTo(other.ColliderId);
				if (result != 0) {
					return result;
				}
				result = IsKinematic.CompareTo(other.IsKinematic);
				return result != 0 ? result : ContactIndex.CompareTo(other.ContactIndex);
			}
		}
	}
}
