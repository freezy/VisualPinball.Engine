// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using NativeTrees;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.Game;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity.Test
{
	public class SpringHingeIntegrationTests
	{
		[Test]
		public void DynamicBroadPhaseRefitsAfterAStationaryBallIsAccelerated()
		{
			var balls = new NativeParallelHashMap<int, BallState>(2, Allocator.Temp);
			var overlaps = new NativeParallelHashSet<int>(2, Allocator.Temp);
			NativeTrees.AABB bounds = new Aabb(new float3(-100f), new float3(100f));
			var octree = new NativeOctree<int>(bounds, 16, 4, Allocator.Temp);
			try {
				balls.Add(1, CreateBall(1, float3.zero, float3.zero));
				balls.Add(2, CreateBall(2, new float3(20f, 0f, 0f), float3.zero));
				PhysicsDynamicBroadPhase.RebuildOctree(ref octree, ref balls);

				var other = balls[2];
				PhysicsDynamicBroadPhase.FindOverlaps(in octree, in other, ref overlaps, ref balls);
				Assert.That(overlaps.Contains(1), Is.False);

				ref var accelerated = ref balls.GetValueByRef(1);
				accelerated.Velocity = new float3(40f, 0f, 0f);
				Assert.That(PhysicsDynamicBroadPhase.RebuildIfMotionEscapes(ref octree,
					ref balls, 0.5f), Is.True);

				PhysicsDynamicBroadPhase.FindOverlaps(in octree, in other, ref overlaps, ref balls);
				Assert.That(overlaps.Contains(1), Is.True,
					"the second ball must query the accelerated ball's refitted swept bounds");
			} finally {
				octree.Dispose();
				overlaps.Dispose();
				balls.Dispose();
			}
		}

		[Test]
		public void DynamicBroadPhaseDoesNotRefitWhileInsertedBoundsContainMotion()
		{
			var balls = new NativeParallelHashMap<int, BallState>(1, Allocator.Temp);
			NativeTrees.AABB bounds = new Aabb(new float3(-100f), new float3(100f));
			var octree = new NativeOctree<int>(bounds, 8, 3, Allocator.Temp);
			try {
				balls.Add(1, CreateBall(1, float3.zero, new float3(2f, 0f, 0f)));
				PhysicsDynamicBroadPhase.RebuildOctree(ref octree, ref balls);

				Assert.That(PhysicsDynamicBroadPhase.RebuildIfMotionEscapes(ref octree,
					ref balls, 0.1f), Is.False);
			} finally {
				octree.Dispose();
				balls.Dispose();
			}
		}

		[Test]
		public void SpinCorrectionDoesNotRemoveMomentumFromAttachedBall()
		{
			var ball = CreateBall(1, float3.zero, float3.zero);
			ball.AngularMomentum = new float3(100f, 0f, 0f);
			ball.LastPositions = new BallPositions(new float3(1f, 0f, 0f));
			var freeBall = ball;
			var attachedBall = ball;
			attachedBall.AttachedMagnetId = 20;

			PhysicsCycle.ApplyBallSpinCorrection(ref freeBall);
			PhysicsCycle.ApplyBallSpinCorrection(ref attachedBall);

			Assert.That(math.length(freeBall.AngularMomentum), Is.LessThan(100f));
			Assert.That(attachedBall.AngularMomentum, Is.EqualTo(ball.AngularMomentum));
		}

		[Test]
		public void UnsupportedActiveColliderClassificationExcludesPassiveSurfaces()
		{
			Assert.That(PhysicsStaticCollision.IsUnsupportedActiveCollider(ColliderType.Bumper), Is.True);
			Assert.That(PhysicsStaticCollision.IsUnsupportedActiveCollider(ColliderType.Flipper), Is.True);
			Assert.That(PhysicsStaticCollision.IsUnsupportedActiveCollider(ColliderType.LineSlingShot), Is.True);
			Assert.That(PhysicsStaticCollision.IsUnsupportedActiveCollider(ColliderType.Plunger), Is.True);
			Assert.That(PhysicsStaticCollision.IsUnsupportedActiveCollider(ColliderType.KickerCircle), Is.True);
			Assert.That(PhysicsStaticCollision.IsUnsupportedActiveCollider(ColliderType.Plane), Is.False);
			Assert.That(PhysicsStaticCollision.IsUnsupportedActiveCollider(ColliderType.SpringHinge), Is.False);
		}

		[Test]
		public void HingeImpactUsesThresholdAndDeduplicatesRepeatedPosition()
		{
			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			var collider = CreateCollider(2f);
			var hinge = new SpringHingeState(12, new SpringHingeStaticState {
				OwnerId = 12,
				Pivot = float3.zero,
				Axis = new float3(0f, 0f, 1f),
				Inertia = 10f,
				MinimumAngle = -math.PI,
				MaximumAngle = math.PI
			}, default);
			var ball = CreateBall(1, new float3(10f, 3f, 0f), new float3(0f, -1f, 0f));

			collider.Collide(ref ball, ref hinge, default, ref state);
			Assert.That(harness.EventQueue.Count, Is.Zero);

			ball.Velocity = new float3(0f, -3f, 0f);
			collider.Collide(ref ball, ref hinge, default, ref state);
			ball.Velocity = new float3(0f, -3f, 0f);
			collider.Collide(ref ball, ref hinge, default, ref state);

			Assert.That(harness.EventQueue.Count, Is.EqualTo(1));
			Assert.That(harness.EventQueue.Dequeue().EventId, Is.EqualTo(EventId.HitEventsHit));
		}

		private static SpringHingeCollider CreateCollider(float hitThreshold)
		{
			var pivot = float3.zero;
			var centre = new float3(10f, 0f, 0f);
			var extents = new float3(5f, 2f, 2f);
			var x = new float3(1f, 0f, 0f);
			var y = new float3(0f, 1f, 0f);
			var z = new float3(0f, 0f, 1f);
			return new SpringHingeCollider(12, in pivot, in centre, in extents,
				in x, in y, in z, new ColliderInfo {
					ItemId = 12,
					FireEvents = true,
					HitThreshold = hitThreshold
				});
		}

		private static BallState CreateBall(int id, in float3 position, in float3 velocity)
			=> new() { Id = id, Position = position, Velocity = velocity, Mass = 1f, Radius = 1f };
	}
}
