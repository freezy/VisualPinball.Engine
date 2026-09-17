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

using NativeTrees;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// Moving kinematic items are excluded from the kinematic octree and
	/// broad-phased directly, so the octree is rebuilt when an item starts or
	/// stops moving instead of on every pose update.
	/// </summary>
	public class KinematicBroadPhaseTests
	{
		private const int ItemId = 1;

		[Test]
		public void MovingItemIsSkippedByOctreeAndFoundDirectly()
		{
			var nonTransformable = new NativeParallelHashMap<int, float4x4>(1, Allocator.Persistent);
			var kinematicTransforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Persistent);
			var targets = new NativeParallelHashMap<int, float4x4>(1, Allocator.Persistent);
			var outOfOctree = new NativeParallelHashSet<int>(1, Allocator.Persistent);
			var itemBounds = new NativeParallelHashMap<int, Aabb>(1, Allocator.Persistent);
			var velocities = new NativeParallelHashMap<int, KinematicVelocityState>(1, Allocator.Persistent);
			var overlaps = new NativeParallelHashSet<int>(8, Allocator.Persistent);
			var octree = new NativeOctree<int>(new AABB(new float3(-2000f), new float3(2000f)), 16, 4, Allocator.Persistent);
			var references = new ColliderReference(ref nonTransformable, Allocator.Persistent, true);
			var lookups = default(NativeParallelHashMap<int, NativeColliderIds>);
			var matrix = float4x4.Translate(new float3(100f, 0f, 0f));
			try {
				// a 100 x 100 floor centered at x = 100, at z = 0
				references.Add(new TriangleCollider(new float3(-50f, -50f, 0f), new float3(-50f, 50f, 0f),
					new float3(50f, -50f, 0f), new ColliderInfo { ItemId = ItemId, ItemType = ItemType.Primitive }), matrix);
				using var current = new NativeColliders(ref references, Allocator.Persistent);
				kinematicTransforms.Add(ItemId, matrix);
				targets.Add(ItemId, matrix);
				lookups = references.CreateLookup(Allocator.Persistent);
				references.TransformToIdentity(ref kinematicTransforms);
				using var identity = new NativeColliders(ref references, Allocator.Persistent);

				var state = new PhysicsState {
					KinematicColliders = current,
					KinematicCollidersAtIdentity = identity,
					KinematicTransforms = kinematicTransforms,
					KinematicTargetTransforms = targets,
					KinematicColliderLookups = lookups,
					KinematicVelocities = velocities,
					KinematicItemsOutOfOctree = outOfOctree,
					KinematicMovingItemBounds = itemBounds,
				};
				var ballOnFloor = new BallState { Id = 7, Radius = 25f, Position = new float3(90f, -10f, 25f) };
				var ballFarAway = new BallState { Id = 8, Radius = 25f, Position = new float3(900f, 900f, 25f) };
				const int colliderId = 0;

				// idle item: found through the octree, the direct pass adds nothing
				PhysicsKinematics.RebuildOctree(ref octree, ref state);
				PhysicsStaticBroadPhase.FindOverlaps(in octree, in ballOnFloor, ref overlaps, PhysicsConstants.PhysFactor);
				Assert.That(overlaps.Contains(colliderId), Is.True, "idle item must be in the octree");
				overlaps.Clear();
				PhysicsStaticBroadPhase.FindMovingKinematicOverlaps(ref state, in ballOnFloor, ref overlaps, PhysicsConstants.PhysFactor);
				Assert.That(overlaps.Count(), Is.Zero, "idle item must not be tested directly");

				// moving item: skipped by the octree, found by the direct pass
				outOfOctree.Add(ItemId);
				PhysicsKinematics.RebuildOctree(ref octree, ref state);
				PhysicsStaticBroadPhase.FindOverlaps(in octree, in ballOnFloor, ref overlaps, PhysicsConstants.PhysFactor);
				Assert.That(overlaps.Count(), Is.Zero, "moving item must be excluded from the octree");
				PhysicsStaticBroadPhase.FindMovingKinematicOverlaps(ref state, in ballOnFloor, ref overlaps, PhysicsConstants.PhysFactor);
				Assert.That(overlaps.Contains(colliderId), Is.True, "moving item must be found by the direct pass");

				overlaps.Clear();
				PhysicsStaticBroadPhase.FindMovingKinematicOverlaps(ref state, in ballFarAway, ref overlaps, PhysicsConstants.PhysFactor);
				Assert.That(overlaps.Count(), Is.Zero, "a ball away from the moving item must not get its colliders");

				// item bounds are the union of the collider bounds at the current pose
				// and reject the far ball with a single test
				var colliderIds = state.KinematicColliderLookups[ItemId];
				PhysicsKinematics.UpdateMovingItemBounds(ref state, ItemId, in colliderIds);
				Assert.That(itemBounds.TryGetValue(ItemId, out var bounds), Is.True);
				Assert.That(bounds.Left, Is.EqualTo(50f).Within(1e-4f));
				Assert.That(bounds.Right, Is.EqualTo(150f).Within(1e-4f));
				Assert.That(bounds.IntersectRect(ballOnFloor.GetSweptAabb(PhysicsConstants.PhysFactor)), Is.True);
				Assert.That(bounds.IntersectRect(ballFarAway.GetSweptAabb(PhysicsConstants.PhysFactor)), Is.False);
				overlaps.Clear();
				PhysicsStaticBroadPhase.FindMovingKinematicOverlaps(ref state, in ballOnFloor, ref overlaps, PhysicsConstants.PhysFactor);
				Assert.That(overlaps.Contains(colliderId), Is.True, "item bounds must not reject a ball that overlaps the item");

				// a surface approaching the ball within the tick is admitted even when the
				// ball's own bounds do not yet touch the collider: the floor at z = 0 is
				// reported moving up at 10 units per step, the ball hovers 0.5 above it
				var hoveringBall = new BallState { Id = 9, Radius = 25f, Position = new float3(100f, 0f, 25.5f) };
				overlaps.Clear();
				PhysicsStaticBroadPhase.FindMovingKinematicOverlaps(ref state, in hoveringBall, ref overlaps, PhysicsConstants.PhysFactor);
				Assert.That(overlaps.Count(), Is.Zero, "without surface velocity a hovering ball is out of reach");
				velocities.Add(ItemId, new KinematicVelocityState {
					LinearVelocity = new float3(0f, 0f, 10f),
					Pivot = matrix.c3.xyz,
				});
				PhysicsStaticBroadPhase.FindMovingKinematicOverlaps(ref state, in hoveringBall, ref overlaps, PhysicsConstants.PhysFactor);
				Assert.That(overlaps.Contains(colliderId), Is.True, "surface velocity must extend the query by one tick of motion");
				velocities.Remove(ItemId);

				// bounds are not maintained for idle items
				outOfOctree.Remove(ItemId);
				itemBounds.Remove(ItemId);
				PhysicsKinematics.UpdateMovingItemBounds(ref state, ItemId, in colliderIds);
				Assert.That(itemBounds.ContainsKey(ItemId), Is.False);
			} finally {
				if (lookups.IsCreated) {
					using (var enumerator = lookups.GetEnumerator()) {
						while (enumerator.MoveNext()) {
							enumerator.Current.Value.Dispose();
						}
					}
					lookups.Dispose();
				}
				references.Dispose();
				octree.Dispose();
				overlaps.Dispose();
				velocities.Dispose();
				itemBounds.Dispose();
				outOfOctree.Dispose();
				targets.Dispose();
				kinematicTransforms.Dispose();
				nonTransformable.Dispose();
			}
		}

		[Test]
		public void UncreatedMovingItemSetTreatsEveryItemAsIdle()
		{
			var overlaps = new NativeParallelHashSet<int>(8, Allocator.Persistent);
			try {
				var state = new PhysicsState();
				var ball = new BallState { Id = 7, Radius = 25f, Position = new float3(90f, -10f, 25f) };
				PhysicsStaticBroadPhase.FindMovingKinematicOverlaps(ref state, in ball, ref overlaps, PhysicsConstants.PhysFactor);
				Assert.That(overlaps.Count(), Is.Zero);
			} finally {
				overlaps.Dispose();
			}
		}
	}
}
