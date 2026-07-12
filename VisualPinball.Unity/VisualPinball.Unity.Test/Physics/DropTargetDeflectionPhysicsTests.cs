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
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity.Test
{
	public class DropTargetDeflectionPhysicsTests
	{
		[Test]
		public void SlidingBladeUsesFaceMassAtEveryPoint()
		{
			var target = CreateTarget(DropTargetDeflectionKind.SlidingBlade);

			var data = DropTargetDeflectionPhysics.AtPoint(in target.Static,
				in target.Mechanical, new float3(0f, 20f, 0f));

			Assert.That(data.VelocityJacobian.x, Is.EqualTo(-1f).Within(1e-5f));
			Assert.That(data.InverseGeneralizedMass, Is.EqualTo(5f).Within(1e-5f));
		}

		[Test]
		public void HingedReferencePointReproducesConfiguredEffectiveMass()
		{
			var target = CreateTarget(DropTargetDeflectionKind.HingedBlade);
			var referencePoint = new float3(0f, 10f, 0f);

			var data = DropTargetDeflectionPhysics.AtPoint(in target.Static,
				in target.Mechanical, in referencePoint);
			var normalJacobian = math.dot(data.VelocityJacobian, new float3(1f, 0f, 0f));

			Assert.That(normalJacobian * normalJacobian * data.InverseGeneralizedMass,
				Is.EqualTo(1f / target.Static.Mechanical.EffectiveFaceMass).Within(1e-5f));
			Assert.That(math.dot(data.VelocityJacobian, -target.Static.FaceNormal),
				Is.GreaterThan(0f));
		}

		[Test]
		public void HingedOffCenterContactHasPositionDependentEffectiveMass()
		{
			var target = CreateTarget(DropTargetDeflectionKind.HingedBlade);
			var reference = DropTargetDeflectionPhysics.AtPoint(in target.Static,
				in target.Mechanical, new float3(0f, 10f, 0f));
			var offCenter = DropTargetDeflectionPhysics.AtPoint(in target.Static,
				in target.Mechanical, new float3(0f, 20f, 0f));

			Assert.That(math.lengthsq(offCenter.VelocityJacobian),
				Is.EqualTo(4f * math.lengthsq(reference.VelocityJacobian)).Within(1e-5f));
		}

		[Test]
		public void HingedImpactUsesContactPointEffectiveMass()
		{
			var referenceTarget = CreateTarget(DropTargetDeflectionKind.HingedBlade);
			var offCenterTarget = referenceTarget;
			var referenceBall = new BallState {
				Mass = 1f,
				Radius = 25f,
				Position = new float3(25f, 10f, 0f),
				Velocity = new float3(-30f, 0f, 0f),
			};
			var offCenterBall = referenceBall;
			offCenterBall.Position.y = 20f;

			var referenceResult = MechanicalDropTargetPhysics.ResolveImpact(ref referenceBall,
				ref referenceTarget.Mechanical, in referenceTarget.Static,
				new float3(1f, 0f, 0f), 0.35f, 0f);
			var offCenterResult = MechanicalDropTargetPhysics.ResolveImpact(ref offCenterBall,
				ref offCenterTarget.Mechanical, in offCenterTarget.Static,
				new float3(1f, 0f, 0f), 0.35f, 0f);

			Assert.That(referenceResult.Applied, Is.True);
			Assert.That(offCenterResult.Applied, Is.True);
			Assert.That(offCenterResult.NormalImpulse, Is.LessThan(referenceResult.NormalImpulse));
			Assert.That(offCenterBall.Velocity.x, Is.LessThan(referenceBall.Velocity.x));
		}

		private static DropTargetState CreateTarget(DropTargetDeflectionKind kind)
		{
			var config = DropTargetMechanicalConfig.Default;
			config.DeflectionKind = kind;
			config.DeflectionAxis = Vector3.forward;
			config.DeflectionPivot = Vector3.zero;
			config.ReferenceContactPoint = new Vector3(0f, 10f, 0f);
			return new DropTargetState(0, new DropTargetStaticState {
				PhysicsMode = DropTargetPhysicsMode.Mechanical,
				FaceNormal = new float3(1f, 0f, 0f),
				Mechanical = config,
			}, default) {
				Mechanical = new DropTargetMechanicalState {
					State = DropTargetMechanismState.Latched,
					PoseInitialized = true,
					BaseTransform = float4x4.identity,
				}
			};
		}
	}
}
