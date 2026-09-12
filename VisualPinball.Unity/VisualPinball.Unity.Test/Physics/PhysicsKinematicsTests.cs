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

using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace VisualPinball.Unity.Test
{
	public class PhysicsKinematicsTests
	{
		[Test]
		public void LinearCatchUpSpeedDoesNotCompound()
		{
			const int itemId = 1;
			const ulong appliedTimeUsec = 10_000_000;
			const float measuredSpeed = 0.38f;
			using var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			using var targets = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			using var velocities = new NativeParallelHashMap<int, KinematicVelocityState>(1, Allocator.Temp);
			using var colliderLookups = new NativeParallelHashMap<int, NativeColliderIds>(1, Allocator.Temp);

			transforms.Add(itemId, float4x4.identity);
			targets.Add(itemId, float4x4.Translate(new float3(60f, 0f, 0f)));
			velocities.Add(itemId, new KinematicVelocityState {
				LinearVelocity = new float3(measuredSpeed, 0f, 0f),
				LastUpdateUsec = 1_000_000,
				LastAppliedUsec = appliedTimeUsec,
				PaceSpeed = measuredSpeed,
			});

			var state = new PhysicsState {
				KinematicTransforms = transforms,
				KinematicTargetTransforms = targets,
				KinematicVelocities = velocities,
				KinematicColliderLookups = colliderLookups,
			};
			var maximumCatchUpSpeed = measuredSpeed * 1.25f;

			for (var i = 0; i < 40; i++) {
				PhysicsKinematics.StepKinematics(ref state, appliedTimeUsec + (ulong)i * 1_000);
				var velocity = velocities[itemId];
				Assert.That(math.length(velocity.StepVelocity), Is.LessThanOrEqualTo(maximumCatchUpSpeed + 1e-5f));
				Assert.That(math.length(state.GetKinematicVelocityAt(itemId, float3.zero)),
					Is.LessThanOrEqualTo(maximumCatchUpSpeed + 1e-5f));
			}
		}

		[Test]
		public void SettledCatchUpClearsStepAndPace()
		{
			const int itemId = 1;
			const ulong appliedTimeUsec = 10_000_000;
			const float measuredSpeed = 0.38f;
			using var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			using var targets = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			using var velocities = new NativeParallelHashMap<int, KinematicVelocityState>(1, Allocator.Temp);
			using var colliderLookups = new NativeParallelHashMap<int, NativeColliderIds>(1, Allocator.Temp);
			var target = float4x4.Translate(new float3(60f, 0f, 0f));

			transforms.Add(itemId, float4x4.identity);
			targets.Add(itemId, target);
			velocities.Add(itemId, new KinematicVelocityState {
				LinearVelocity = new float3(measuredSpeed, 0f, 0f),
				LastUpdateUsec = 1_000_000,
				LastAppliedUsec = appliedTimeUsec,
				PaceSpeed = measuredSpeed,
			});

			var state = new PhysicsState {
				KinematicTransforms = transforms,
				KinematicTargetTransforms = targets,
				KinematicVelocities = velocities,
				KinematicColliderLookups = colliderLookups,
			};

			for (var i = 0; i < 900; i++) {
				PhysicsKinematics.StepKinematics(ref state, appliedTimeUsec + (ulong)i * 1_000);
			}

			var velocity = velocities[itemId];
			Assert.That(transforms[itemId], Is.EqualTo(target));
			Assert.That(velocity.StepVelocity, Is.EqualTo(float3.zero));
			Assert.That(velocity.PaceSpeed, Is.Zero);
			Assert.That(velocity.PaceAngularSpeed, Is.Zero);
		}

		[Test]
		public void AngularCatchUpSpeedDoesNotCompound()
		{
			const int itemId = 1;
			const ulong appliedTimeUsec = 10_000_000;
			const float measuredSpeed = 0.009f;
			using var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			using var targets = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			using var velocities = new NativeParallelHashMap<int, KinematicVelocityState>(1, Allocator.Temp);
			using var colliderLookups = new NativeParallelHashMap<int, NativeColliderIds>(1, Allocator.Temp);

			transforms.Add(itemId, float4x4.identity);
			targets.Add(itemId, float4x4.RotateZ(math.radians(7.5f)));
			velocities.Add(itemId, new KinematicVelocityState {
				AngularVelocity = new float3(0f, 0f, measuredSpeed),
				LastUpdateUsec = 1_000_000,
				LastAppliedUsec = appliedTimeUsec,
				PaceAngularSpeed = measuredSpeed,
			});

			var state = new PhysicsState {
				KinematicTransforms = transforms,
				KinematicTargetTransforms = targets,
				KinematicVelocities = velocities,
				KinematicColliderLookups = colliderLookups,
			};
			var maximumCatchUpSpeed = measuredSpeed * 1.25f;

			for (var i = 0; i < 10; i++) {
				PhysicsKinematics.StepKinematics(ref state, appliedTimeUsec + (ulong)i * 1_000);
				var velocity = velocities[itemId];
				Assert.That(math.length(velocity.StepAngularVelocity), Is.LessThanOrEqualTo(maximumCatchUpSpeed + 1e-5f));
				Assert.That(math.length(state.GetKinematicVelocityAt(itemId, new float3(1f, 0f, 0f))),
					Is.LessThanOrEqualTo(maximumCatchUpSpeed + 1e-5f));
			}
		}

		[Test]
		public void VelocitylessTargetSnapsWithoutSurfaceVelocity()
		{
			const int itemId = 1;
			using var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			using var targets = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			using var velocities = new NativeParallelHashMap<int, KinematicVelocityState>(1, Allocator.Temp);
			using var colliderLookups = new NativeParallelHashMap<int, NativeColliderIds>(1, Allocator.Temp);
			var target = float4x4.Translate(new float3(30f, 0f, 0f));

			transforms.Add(itemId, float4x4.identity);
			targets.Add(itemId, target);
			velocities.Add(itemId, new KinematicVelocityState { LastAppliedUsec = 10_000_000 });

			var state = new PhysicsState {
				KinematicTransforms = transforms,
				KinematicTargetTransforms = targets,
				KinematicVelocities = velocities,
				KinematicColliderLookups = colliderLookups,
			};

			PhysicsKinematics.StepKinematics(ref state, 10_000_000);

			Assert.That(transforms[itemId], Is.EqualTo(target));
			Assert.That(state.GetKinematicVelocityAt(itemId, target.c3.xyz), Is.EqualTo(float3.zero));
		}

		[Test]
		public void PacelessLinearAxisSnapsInsteadOfUsingCatchUpCeiling()
		{
			const int itemId = 1;
			using var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			using var targets = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			using var velocities = new NativeParallelHashMap<int, KinematicVelocityState>(1, Allocator.Temp);
			using var colliderLookups = new NativeParallelHashMap<int, NativeColliderIds>(1, Allocator.Temp);
			var target = math.mul(float4x4.Translate(new float3(30f, 0f, 0f)),
				float4x4.RotateZ(math.radians(7.5f)));

			transforms.Add(itemId, float4x4.identity);
			targets.Add(itemId, target);
			velocities.Add(itemId, new KinematicVelocityState {
				AngularVelocity = new float3(0f, 0f, 0.009f),
				LastAppliedUsec = 10_000_000,
				PaceAngularSpeed = 0.009f,
			});

			var state = new PhysicsState {
				KinematicTransforms = transforms,
				KinematicTargetTransforms = targets,
				KinematicVelocities = velocities,
				KinematicColliderLookups = colliderLookups,
			};

			PhysicsKinematics.StepKinematics(ref state, 10_000_000);

			var velocity = velocities[itemId];
			Assert.That(transforms[itemId], Is.EqualTo(target));
			Assert.That(velocity.StepVelocity, Is.EqualTo(float3.zero));
			Assert.That(velocity.StepAngularVelocity, Is.EqualTo(float3.zero));
		}

		[Test]
		public void SettledPoseExpiresVelocityWhenTransformProducerStalls()
		{
			const int itemId = 1;
			const ulong sampleTimeUsec = 1_000_000;
			const ulong appliedTimeUsec = 10_000_000;
			using var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			using var targets = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			using var velocities = new NativeParallelHashMap<int, KinematicVelocityState>(1, Allocator.Temp);
			using var colliderLookups = new NativeParallelHashMap<int, NativeColliderIds>(1, Allocator.Temp);

			var pose = float4x4.Translate(new float3(100f, 200f, 300f));
			transforms.Add(itemId, pose);
			targets.Add(itemId, pose);
			velocities.Add(itemId, new KinematicVelocityState {
				LinearVelocity = new float3(2f, 3f, -4f),
				AngularVelocity = new float3(0.1f, 0.2f, 0.3f),
				Pivot = pose.c3.xyz,
				LastUpdateUsec = sampleTimeUsec,
				LastAppliedUsec = appliedTimeUsec,
			});

			var state = new PhysicsState {
				KinematicTransforms = transforms,
				KinematicTargetTransforms = targets,
				KinematicVelocities = velocities,
				KinematicColliderLookups = colliderLookups,
			};

			PhysicsKinematics.StepKinematics(ref state,
				appliedTimeUsec + PhysicsKinematics.KinematicVelocityTimeoutUsec - 1);
			Assert.That(state.GetKinematicVelocityAt(itemId, pose.c3.xyz), Is.EqualTo(new float3(2f, 3f, -4f)));

			PhysicsKinematics.StepKinematics(ref state,
				appliedTimeUsec + PhysicsKinematics.KinematicVelocityTimeoutUsec);
			Assert.That(state.GetKinematicVelocityAt(itemId, pose.c3.xyz), Is.EqualTo(float3.zero));
		}
	}
}
