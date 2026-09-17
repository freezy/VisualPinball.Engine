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
	/// <summary>
	/// Per-update work counters of the physics cycle, for diagnostics. Reset at
	/// the start of every <see cref="PhysicsUpdate.Execute"/>.
	/// </summary>
	public struct PhysicsCounters
	{
		/// <summary>Inner cycle iterations (hit-time sub steps) across all ticks of the update.</summary>
		public int Iterations;
		/// <summary>Static and kinematic collider hit tests.</summary>
		public int HitTests;
		/// <summary>Ball-ball hit tests.</summary>
		public int BallTests;
		/// <summary>Contacts handed to the contact solver.</summary>
		public int Contacts;
		/// <summary>Octree objects visited (bounds tests) by the static and kinematic broad phases.</summary>
		public int BroadPhaseVisits;
		/// <summary>Most collider hit tests one ball needed in one iteration.</summary>
		public int MaxBallHitTests;
		public int MaxBallId;
		public float3 MaxBallPosition;
		public int Triangle;
		public int Line3D;
		public int Line;
		public int Point;
		public int Plane;
		public int Circle;
		public int Flipper;
		public int Other;

		internal void CountHitTest(ColliderType type)
		{
			HitTests++;
			switch (type) {
				case ColliderType.Triangle: Triangle++; break;
				case ColliderType.Line3D: Line3D++; break;
				case ColliderType.Line:
				case ColliderType.LineZ:
				case ColliderType.LineSlingShot: Line++; break;
				case ColliderType.Point: Point++; break;
				case ColliderType.Plane: Plane++; break;
				case ColliderType.Circle:
				case ColliderType.Bumper:
				case ColliderType.KickerCircle:
				case ColliderType.TriggerCircle: Circle++; break;
				case ColliderType.Flipper: Flipper++; break;
				default: Other++; break;
			}
		}
	}

	public struct PhysicsCycle : IDisposable
	{
		private NativeList<ContactBufferElement> _contacts;
		private static readonly ProfilerMarker PerfMarker = new("PhysicsCycle");
		private static readonly ProfilerMarker PerfMarkerDisplacement = new("Displacement");
		private static readonly ProfilerMarker PerfMarkerCollision = new("Collision");
		private static readonly ProfilerMarker PerfMarkerContacts = new("Contacts");
		internal int DynamicBroadPhaseRefitCount { get; private set; }
		internal PhysicsCounters Counters;

		public PhysicsCycle(Allocator a)
		{
			_contacts = new NativeList<ContactBufferElement>(a);
			DynamicBroadPhaseRefitCount = 0;
			Counters = default;
		}

		internal void Simulate(ref PhysicsState state, ref NativeParallelHashSet<int> overlappingColliders, ref NativeOctree<int> kinematicOctree, ref NativeOctree<int> ballOctree, float dTime)
		{
			PerfMarker.Begin();
			var staticCounts = PhysicsConstants.StaticCnts;

			// rebuild octree of ball-to-ball collision (clear + re-insert, no alloc)
			// it's okay to have this code outside of the inner loop, as the ball hitrects already include the maximum distance they can travel in that timespan
			PhysicsDynamicBroadPhase.RebuildOctree(ref ballOctree, ref state.Balls, dTime);

			while (dTime > 0) {
				Counters.Iterations++;

				var hitTime = dTime;       // begin time search from now ...  until delta ends
				var mechanismStopTime = -1f;
				var springHingeHitTime = -1f;

				ApplyFlipperTime(ref hitTime, ref mechanismStopTime, ref state);
				ApplySpringHingeTime(ref hitTime, ref mechanismStopTime, ref state);

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

						// hit testing (overlappingColliders is cleared in broad phase); the
						// broad phase covers the same time window the narrow phase searches
						var hitTestsBefore = Counters.HitTests;
						PhysicsStaticBroadPhase.FindOverlaps(in state.Octree, in ball, ref overlappingColliders, hitTime, ref Counters);
						PhysicsStaticNarrowPhase.FindNextCollision(ref state.Colliders, ref ball, ref overlappingColliders, ref _contacts, ref state, ref Counters);

						PhysicsStaticBroadPhase.FindOverlaps(in kinematicOctree, in ball, ref overlappingColliders, hitTime, ref Counters);
						PhysicsStaticBroadPhase.FindMovingKinematicOverlaps(ref state, in ball, ref overlappingColliders, hitTime);
						PhysicsStaticNarrowPhase.FindNextCollision(ref state.KinematicColliders, ref ball, ref overlappingColliders, ref _contacts, ref state, ref Counters);
						RecordSpringHingeHitTime(ref springHingeHitTime, in ball, ref state);

						// no negative time allowed
						if (ball.CollisionEvent.HitTime < 0) {
							ball.CollisionEvent.ClearCollider();
						}

						PhysicsDynamicBroadPhase.FindOverlaps(in ballOctree, in ball, ref overlappingColliders, ref state.Balls, hitTime);
						PhysicsDynamicNarrowPhase.FindNextCollision(ref ball, ref overlappingColliders, ref _contacts, ref state, ref Counters);

						var ballHitTests = Counters.HitTests - hitTestsBefore;
						if (ballHitTests > Counters.MaxBallHitTests) {
							Counters.MaxBallHitTests = ballHitTests;
							Counters.MaxBallId = ball.Id;
							Counters.MaxBallPosition = ball.Position;
						}

						// apply static time
						ApplyStaticTime(ref hitTime, ref staticCounts, in ball);
					}
				}
				Counters.Contacts += _contacts.Length;
				ClampToMechanismStop(ref hitTime, mechanismStopTime);
				ClampToSpringHingeHit(ref hitTime, springHingeHitTime);

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
				// spring hinges
				using (var enumerator = state.SpringHingeStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var hingeState = ref enumerator.Current.Value;
						SpringHingeDisplacementPhysics.UpdateDisplacement(ref hingeState, hitTime);
					}
				}

				PerfMarkerDisplacement.End();
				#endregion

				// collision
				PerfMarkerCollision.Begin();
				using (var enumerator = state.Balls.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var ball = ref enumerator.Current.Value;

						// dynamic collision (ball/ball)
						PhysicsDynamicCollision.Collide(hitTime, ref ball, ref state);

						// static & kinematic collision
						PhysicsStaticCollision.Collide(hitTime, ref ball, ref state);
					}
				}
				PerfMarkerCollision.End();

				// handle contacts
				PerfMarkerContacts.Begin();
				PrepareContacts(ref state);
				for (var i = 0; i < _contacts.Length; i++) {
					ref var contact = ref _contacts.GetElementAsRef(i);
					if (contact.IsDuplicate) {
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
						ApplyBallSpinCorrection(ref enumerator.Current.Value);
					}
				}

				dTime -= hitTime;
				if (PhysicsDynamicBroadPhase.RebuildIfMotionEscapes(ref ballOctree,
					    ref state.Balls, dTime)) {
					DynamicBroadPhaseRefitCount++;
				}

				state.SwapBallCollisionHandling = !state.SwapBallCollisionHandling;
			}

			PerfMarker.End();
		}

		internal void ResetDynamicBroadPhaseRefitCount()
		{
			DynamicBroadPhaseRefitCount = 0;
			Counters = default;
		}

		internal static void ApplyBallSpinCorrection(ref BallState ball)
		{
			if (ball.AttachedMagnetId == 0) {
				BallSpinHackPhysics.Update(ref ball);
			}
		}

		private void PrepareContacts(ref PhysicsState state)
		{
			for (var i = 0; i < _contacts.Length; i++) {
				ref var contact = ref _contacts.GetElementAsRef(i);
				ref var ball = ref state.Balls.GetValueByRef(contact.BallId);
				contact.FrictionVelocity = ball.Velocity;
				contact.FrictionAngularMomentum = ball.AngularMomentum;
				contact.FrictionAcceleration = state.Env.Gravity + ball.ExternalAcceleration;
				contact.IsDuplicate = false;
			}

			for (var i = 0; i < _contacts.Length; i++) {
				ref var contact = ref _contacts.GetElementAsRef(i);
				contact.IsDuplicate = IsDuplicateContact(i, ref state);
			}

			for (var i = 0; i < _contacts.Length; i++) {
				ref var contact = ref _contacts.GetElementAsRef(i);
				if (contact.IsDuplicate) {
					continue;
				}
				var frictionAcceleration = contact.FrictionAcceleration;
				// Project the load against the other active contact half-spaces. Reusing
				// the running remainder is exact for orthogonal contacts and converges for
				// wedges; using the original acceleration for every normal double-counts
				// support at oblique multi-contact corners.
				for (var pass = 0; pass < 8; pass++) {
					var removedSupport = false;
					for (var j = 0; j < _contacts.Length; j++) {
						if (i == j) {
							continue;
						}
						var other = _contacts[j];
						if (other.IsDuplicate || other.BallId != contact.BallId || other.CollEvent.ColliderId < 0) {
							continue;
						}
						var normal = GetContactNormalInPlayfield(in other, ref state);
						var supported = ContactPhysics.SupportedAcceleration(in frictionAcceleration, in normal);
						if (math.lengthsq(supported) <= 1e-10f) {
							continue;
						}
						frictionAcceleration -= supported;
						removedSupport = true;
					}
					if (!removedSupport) {
						break;
					}
				}
				contact.FrictionAcceleration = frictionAcceleration;
			}
		}

		private bool IsDuplicateContact(int contactIndex, ref PhysicsState state)
		{
			var current = _contacts[contactIndex];
			if (current.CollEvent.ColliderId < 0) {
				return false;
			}

			ref var currentColliders = ref (current.CollEvent.IsKinematic
				? ref state.KinematicColliders
				: ref state.Colliders);
			ref var currentHeader = ref state.GetColliderHeader(ref currentColliders, current.CollEvent.ColliderId);
			for (var i = 0; i < contactIndex; i++) {
				var previous = _contacts[i];
				if (previous.IsDuplicate || previous.CollEvent.ColliderId < 0 ||
				    previous.CollEvent.IsKinematic != current.CollEvent.IsKinematic) {
					continue;
				}
				ref var previousColliders = ref (previous.CollEvent.IsKinematic
					? ref state.KinematicColliders
					: ref state.Colliders);
				ref var previousHeader = ref state.GetColliderHeader(ref previousColliders, previous.CollEvent.ColliderId);
				if (ContactPhysics.IsDuplicateContact(in current, in currentHeader, in previous, in previousHeader)) {
					return true;
				}
			}
			return false;
		}

		private static float3 GetContactNormalInPlayfield(in ContactBufferElement contact, ref PhysicsState state)
		{
			var normal = contact.CollEvent.HitNormal;
			ref var colliders = ref (contact.CollEvent.IsKinematic
				? ref state.KinematicColliders
				: ref state.Colliders);
			if (!colliders.IsTransformed(contact.CollEvent.ColliderId)) {
				ref var matrix = ref state.GetNonTransformableColliderMatrix(contact.CollEvent.ColliderId, ref colliders);
				normal = matrix.MultiplyVector(normal);
			}
			return math.normalizesafe(normal);
		}
		
		internal static void ApplyStaticTime(ref float hitTime, ref float staticCounts, in BallState ball)
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

		internal static void RecordSpringHingeHitTime(ref float springHingeHitTime,
			in BallState ball, ref PhysicsState state)
		{
			var collEvent = ball.CollisionEvent;
			if (!collEvent.HasCollider() || collEvent.HitTime <= 0f) {
				return;
			}
			ref var colliders = ref (collEvent.IsKinematic
				? ref state.KinematicColliders
				: ref state.Colliders);
			if (colliders.GetHeader(collEvent.ColliderId).Type != ColliderType.SpringHinge) {
				return;
			}
			if (springHingeHitTime <= 0f || collEvent.HitTime < springHingeHitTime) {
				springHingeHitTime = collEvent.HitTime;
			}
		}

		internal static void ClampToSpringHingeHit(ref float hitTime, float springHingeHitTime)
		{
			if (springHingeHitTime > 0f && hitTime > springHingeHitTime) {
				hitTime = springHingeHitTime;
			}
		}

		private void ApplyFlipperTime(ref float hitTime, ref float mechanismStopTime, ref PhysicsState state)
		{
			// for each flipper
			using (var enumerator = state.FlipperStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var flipperState = ref enumerator.Current.Value;
					var flipperHitTime = flipperState.Movement.GetHitTime(flipperState.Static.AngleStart, flipperState.Tricks.AngleEnd);

					// if flipper comes to a rest before the end of the cycle, advance to that time
					if (flipperHitTime > 0 && flipperHitTime < hitTime) { //!! >= 0.f causes infinite loop
						hitTime = flipperHitTime;
						mechanismStopTime = flipperHitTime;
					}
				}
			}
		}

		private static void ApplySpringHingeTime(ref float hitTime, ref float mechanismStopTime,
			ref PhysicsState state)
		{
			using (var enumerator = state.SpringHingeStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					var hingeHitTime = SpringHingeDisplacementPhysics.GetStopTime(enumerator.Current.Value);
					if (hingeHitTime > 0f && hingeHitTime < hitTime) {
						hitTime = hingeHitTime;
						mechanismStopTime = hingeHitTime;
					}
				}
			}
		}

		internal static void ClampToMechanismStop(ref float hitTime, float mechanismStopTime)
		{
			if (mechanismStopTime > 0f && hitTime > mechanismStopTime) {
				hitTime = mechanismStopTime;
			}
		}

		public void Dispose()
		{
			_contacts.Dispose();
		}
	}
}
