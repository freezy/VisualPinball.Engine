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

using Unity.Mathematics;

namespace VisualPinball.Unity
{
	internal enum RothDropTargetOutcome : byte
	{
		None,
		FaceDrop,
		Brick,
		SideHit,
		BacksideDrop,
		BacksideBounce,
	}

	internal static class RothDropTargetPhysics
	{
		internal static RothDropTargetOutcome Classify(in DropTargetRothConfig config, ColliderRole role,
			float approachSpeed, float faceAlignment, float centerDistance)
		{
			if (role == ColliderRole.DropTargetBackFace) {
				return config.EnableBacksideDrop && approachSpeed > config.BacksideVelocity
					? RothDropTargetOutcome.BacksideDrop
					: RothDropTargetOutcome.BacksideBounce;
			}
			if (faceAlignment < 0.5f || approachSpeed <= 0f) {
				return RothDropTargetOutcome.SideHit;
			}
			if (config.EnableBrick && config.BrickVelocity > 0f
				&& approachSpeed > config.BrickVelocity && centerDistance < config.BrickCenterDistance) {
				return RothDropTargetOutcome.Brick;
			}
			return RothDropTargetOutcome.FaceDrop;
		}

		internal static void ApplyMassCorrection(ref BallState ball, in float3 preImpactVelocity,
			in float3 faceNormal, float targetMass)
		{
			var normal = math.normalizesafe(faceNormal);
			var normalVelocity = math.dot(preImpactVelocity, normal);
			var tangentVelocity = preImpactVelocity - normalVelocity * normal;
			var denominator = ball.Mass + targetMass;
			if (ball.Mass <= 0f || targetMass < 0f || denominator <= 0f) {
				return;
			}
			var correctedNormalVelocity = normalVelocity * (ball.Mass - targetMass) / denominator;
			ball.Velocity = tangentVelocity + correctedNormalVelocity * normal;
		}

		internal static void ApplyVerticalBouncer(ref BallState ball, in DropTargetRothConfig config,
			int itemId, uint hitCounter)
		{
			if (!config.EnableVerticalBouncer) {
				return;
			}
			// Match VPW TargetBouncer: BallSpeed is XY speed and velz is replaced,
			// not combined with an existing vertical component.
			var xySpeed = math.length(ball.Velocity.xy);
			if (xySpeed <= 0f) {
				return;
			}
			var multiplier = BouncerMultiplier(StableIndex(config.DeterministicSeed, itemId, ball.Id, hitCounter));
			var z = math.min(math.abs(xySpeed * multiplier * config.VerticalBouncerDeflection
				* config.VerticalBouncerFactor), xySpeed * 0.999f);
			var newXySpeed = math.sqrt(math.max(xySpeed * xySpeed - z * z, 0f));
			ball.Velocity.xy = math.normalizesafe(ball.Velocity.xy) * newXySpeed;
			ball.Velocity.z = z;
		}

		private static int StableIndex(int seed, int itemId, int ballId, uint hitCounter)
		{
			var hash = (uint)seed;
			hash = (hash ^ (uint)itemId) * 16777619u;
			hash = (hash ^ (uint)ballId) * 16777619u;
			hash = (hash ^ hitCounter) * 16777619u;
			return (int)(hash % 6u);
		}

		private static float BouncerMultiplier(int index)
		{
			return index switch {
				0 => 0.2f,
				1 => 0.25f,
				2 => 0.3f,
				3 => 0.4f,
				4 => 0.45f,
				_ => 0.5f,
			};
		}
	}
}
