// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

// ReSharper disable InconsistentNaming

using System;
using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	internal struct RollingSupportFrame { public float3 LocalDirection; public float3 StaticDirection; }
	internal struct RollingSample { public int Surface; public float Weight; public double Time; }
	internal struct RollingCandidate { public int Surface; public int ItemId; public float Weight; public float Speed; public float3 Velocity; }
	internal struct RollingBallState
	{
		public int BallId;
		public uint Generation;
		public int Surface;
		public bool Frozen;
		public bool Supported;
		public bool ReservedSupport;
		public int SupportItemId;
		public float DepartureSpeed;
		public float3 DeparturePosition;
		public int ReservedSurface;
		public double LastSupportTime;
		public float Speed;
		public float3 Velocity;
		public float3 Position;
		public FixedList4096Bytes<RollingSample> History;
	}
	internal struct RollingTransition
	{
		public long Sequence;
		public int BallId;
		public uint Generation;
		public int From;
		public int To;
		public double Time;
		public float Speed;
		public float3 Position;
		public bool Air;
	}

	/// <summary>Bounded FIFO, with cursors in native storage so Burst copies share ownership.</summary>
	internal struct RollingTransitionRing : IDisposable
	{
		internal NativeArray<RollingTransition> Entries;
		// Read cursor, write cursor, monotonically increasing overflow count.
		internal NativeArray<long> Cursors;
		public RollingTransitionRing(int capacity, Allocator allocator) { Entries = new NativeArray<RollingTransition>(capacity, allocator); Cursors = new NativeArray<long>(3, allocator); }
		public void Push(RollingTransition entry)
		{
			var write = Cursors[1];
			if (write - Cursors[0] >= Entries.Length) { Cursors[0]++; Cursors[2]++; }
			entry.Sequence = write;
			Entries[(int)(write % Entries.Length)] = entry;
			Cursors[1] = write + 1;
		}
		public void EnsureCapacity(int capacity)
		{
			if (Entries.Length >= capacity) return;
			var entries = new NativeArray<RollingTransition>(capacity, Allocator.Persistent);
			for (var sequence = Cursors[0]; sequence < Cursors[1]; sequence++) entries[(int)(sequence % capacity)] = Entries[(int)(sequence % Entries.Length)];
			Entries.Dispose(); Entries = entries;
		}

		public bool TryPop(long through, out RollingTransition entry)
		{
			var read = Cursors[0];
			if (read >= through) { entry = default; return false; }
			entry = Entries[(int)(read % Entries.Length)]; Cursors[0] = read + 1; return true;
		}
		public void Dispose() { if (Entries.IsCreated) Entries.Dispose(); if (Cursors.IsCreated) Cursors.Dispose(); }
	}

	internal struct RollingPhysicsState : IDisposable
	{
		public NativeParallelHashMap<int, RollingBallState> Balls;
		public NativeParallelHashMap<int, RollingSupportFrame> SupportFrames;
		public RollingTransitionRing Transitions;
		public bool IsCreated => Balls.IsCreated;
		public RollingPhysicsState(int capacity, int transitionCapacity, Allocator allocator)
		{
			Balls = new NativeParallelHashMap<int, RollingBallState>(capacity, allocator);
			SupportFrames = new NativeParallelHashMap<int, RollingSupportFrame>(0, allocator);
			Transitions = new RollingTransitionRing(math.max(1024, transitionCapacity), allocator);
		}
		public void RegisterBall(int id, uint generation)
		{
			// Only called on registration while PhysicsLock fences the producer. The staging reader owns managed copies.
			if (Balls.Count() >= Balls.Capacity) Balls.Capacity = math.max(128, Balls.Capacity * 2);
			Transitions.EnsureCapacity(math.max(1024, 8 * Balls.Capacity));
			Balls[id] = new RollingBallState { BallId = id, Generation = generation };
		}
		public void Dispose() { if (!IsCreated) return; Balls.Dispose(); SupportFrames.Dispose(); Transitions.Dispose(); }
	}

	/// <summary>Read-only observation after contact resolution. Never feeds forces back into physics.</summary>
	internal static class RollingPhysics
	{
		internal const double HistorySeconds = 0.01;
		internal const double SeamSeconds = 0.02;

		internal static bool IsSupport(float3 normal, float3 gravity, float distance, float normalVelocity)
		{
			if (!math.all(math.isfinite(normal)) || !math.all(math.isfinite(gravity)) || !math.isfinite(distance) || !math.isfinite(normalVelocity)) return false;
			return math.abs(distance) <= PhysicsConstants.PhysTouch && normalVelocity <= PhysicsConstants.ContactVel
				&& math.dot(math.normalizesafe(normal), -math.normalizesafe(gravity)) > 0.1f;
		}

		internal static void Observe(ref PhysicsState state, ref NativeList<ContactBufferElement> contacts, float step, double time)
		{
			if (!state.Rolling.IsCreated) return;
			using var balls = state.Balls.GetEnumerator();
			while (balls.MoveNext()) {
				ref var ball = ref balls.Current.Value;
				if (!state.Rolling.Balls.TryGetValue(ball.Id, out var rolling)) continue;
				rolling.Position = ball.Position;
				rolling.Frozen = ball.IsFrozen;
				if (ball.IsFrozen) {
					rolling.Supported = rolling.ReservedSupport = false; rolling.SupportItemId = 0;
					rolling.Surface = rolling.ReservedSurface = 0; rolling.Speed = 0; rolling.Velocity = default; rolling.History.Clear();
					state.Rolling.Balls[ball.Id] = rolling; continue;
				}
				var candidates = new FixedList4096Bytes<RollingCandidate>();
				for (var i = 0; i < contacts.Length; i++) {
					var contact = contacts[i];
					if (contact.BallId != ball.Id || contact.IsDuplicate || contact.CollEvent.ColliderId < 0 || !contact.CollEvent.IsContact) continue;
					var evt = contact.CollEvent;
					ref var colliders = ref (evt.IsKinematic ? ref state.KinematicColliders : ref state.Colliders);
					ref var header = ref state.GetColliderHeader(ref colliders, evt.ColliderId);
					if (state.DisabledCollisionItems.Contains(header.ItemId)) continue;
					var normal = math.normalizesafe(evt.HitNormal);
					var point = ball.Position - normal * ball.Radius;
					float3 surfaceVelocity;
					if (!colliders.IsTransformed(evt.ColliderId)) {
						ref var matrix = ref state.GetNonTransformableColliderMatrix(evt.ColliderId, ref colliders);
						var localPoint = math.inverse(matrix).MultiplyPoint(point);
						surfaceVelocity = matrix.MultiplyVector(SurfaceVelocity(ref state, ref colliders, in evt, localPoint));
					} else surfaceVelocity = SurfaceVelocity(ref state, ref colliders, in evt, point);
					var velocity = ball.Velocity - surfaceVelocity;
					var gravity = state.Env.Gravity;
					if (state.Rolling.SupportFrames.TryGetValue(header.ItemId, out var frame)) {
						var direction = frame.StaticDirection;
						if (evt.IsKinematic && state.KinematicTransforms.TryGetValue(header.ItemId, out var matrix)) direction = matrix.MultiplyVector(frame.LocalDirection);
						// Acoustic frames own their baked direction; the non-transformable collider map is not their registry.
						gravity = math.normalizesafe(direction) * math.length(state.Env.Gravity);
					}
					// Eligibility uses the post-collision, pre-contact snapshot: support impulses can add a separating normal velocity.
					// Rolling speed still uses the resolved ball-center velocity, projected into the support tangent.
					if (!math.all(math.isfinite(velocity)) || !IsSupport(normal, gravity, evt.HitDistance, math.dot(contact.FrictionVelocity - surfaceVelocity, normal))) continue;
					var weight = math.max(0, -math.dot(gravity, normal)) * step;
					var speed = math.length(velocity - normal * math.dot(velocity, normal));
					var index = -1;
					for (var c = 0; c < candidates.Length; c++) if (candidates[c].Surface == header.RollingSurfaceId) { index = c; break; }
					if (index >= 0) { var candidate = candidates[index]; candidate.Weight += weight; candidates[index] = candidate; }
					else if (candidates.Length < candidates.Capacity) candidates.Add(new RollingCandidate { Surface = header.RollingSurfaceId, ItemId = header.ItemId, Weight = weight, Speed = speed, Velocity = velocity });
				}
				Reduce(ref rolling, ref candidates, time, ref state.Rolling.Transitions);
				state.Rolling.Balls[ball.Id] = rolling;
			}
		}

		private static float3 SurfaceVelocity(ref PhysicsState state, ref NativeColliders colliders, in CollisionEventData evt, float3 point)
		{
			var velocity = state.GetKinematicSurfaceVelocity(in evt, point);
			ref var header = ref state.GetColliderHeader(ref colliders, evt.ColliderId);
			if (header.Type == ColliderType.SpringHinge) {
				ref var hinge = ref state.GetSpringHingeState(evt.ColliderId, ref colliders);
				velocity += hinge.Movement.AngularVelocity * math.cross(hinge.Static.Axis, point - hinge.Static.Pivot);
			} else if (header.Type == ColliderType.Flipper) {
				ref var flipper = ref state.GetFlipperState(evt.ColliderId, ref colliders);
				ref var collider = ref colliders.Flipper(evt.ColliderId);
				velocity += collider.SurfaceVelocity(in flipper.Movement, in point);
			}
			return velocity;
		}

		internal static void Reduce(ref RollingBallState ball, ref FixedList4096Bytes<RollingCandidate> candidates, double time, ref RollingTransitionRing transitions)
		{
			// A time window, not an update count: frame rates and substep counts cannot change the boundary behavior.
			for (var i = ball.History.Length - 1; i >= 0; i--) if (time - ball.History[i].Time >= HistorySeconds) ball.History.RemoveAt(i);
			var selected = -1;
			var best = -1f;
			for (var i = 0; i < candidates.Length; i++) {
				var candidate = candidates[i];
				var score = candidate.Weight;
				for (var h = 0; h < ball.History.Length; h++) if (ball.History[h].Surface == candidate.Surface) score += ball.History[h].Weight;
				if (score > best || (score == best && selected >= 0 && (candidate.Surface == ball.Surface || (candidates[selected].Surface != ball.Surface && candidate.Surface < candidates[selected].Surface)))) { best = score; selected = i; }
				if (ball.History.Length == ball.History.Capacity) ball.History.RemoveAt(0);
				ball.History.Add(new RollingSample { Surface = candidate.Surface, Weight = candidate.Weight, Time = time });
			}
			if (selected >= 0) {
				var candidate = candidates[selected];
				var previous = ball.Supported ? ball.Surface : ball.ReservedSurface;
				var wasSupported = ball.Supported || ball.ReservedSupport;
				if (candidate.Surface != previous || !wasSupported) transitions.Push(new RollingTransition { BallId = ball.BallId, Generation = ball.Generation, From = previous, To = candidate.Surface, Time = time, Speed = candidate.Speed, Position = ball.Position, Air = !wasSupported });
				ball.Surface = ball.ReservedSurface = candidate.Surface;
				ball.Supported = ball.ReservedSupport = true; ball.SupportItemId = candidate.ItemId;
				ball.LastSupportTime = time; ball.DepartureSpeed = candidate.Speed; ball.DeparturePosition = ball.Position; ball.Speed = candidate.Speed; ball.Velocity = candidate.Velocity;
			} else {
				ball.Surface = 0; ball.Supported = false; ball.SupportItemId = 0; ball.Speed = 0; ball.Velocity = default;
				if (ball.ReservedSupport && time - ball.LastSupportTime >= SeamSeconds) {
					transitions.Push(new RollingTransition { BallId = ball.BallId, Generation = ball.Generation, From = ball.ReservedSurface, Time = time, Speed = ball.DepartureSpeed, Position = ball.DeparturePosition, Air = true });
					ball.ReservedSurface = 0; ball.ReservedSupport = false; ball.History.Clear();
				}
			}
		}
	}
}
