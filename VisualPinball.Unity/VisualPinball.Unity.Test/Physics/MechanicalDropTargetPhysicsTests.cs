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
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity.Test
{
	public class MechanicalDropTargetPhysicsTests
	{
		private const float Tolerance = 1e-4f;

		[Test]
		public void FiniteMassImpactMatchesClosedFormSanityCase()
		{
			var target = CreateTargetState();
			var ball = new BallState {
				Mass = 1f,
				Radius = 25f,
				Velocity = new float3(-30f, 0f, 0f),
			};

			var result = MechanicalDropTargetPhysics.ResolveImpact(ref ball, ref target.Mechanical,
				in target.Static, new float3(1f, 0f, 0f), 0.35f, 0f);

			Assert.That(result.Applied, Is.True);
			Assert.That(ball.Velocity.x, Is.EqualTo(-23.25f).Within(Tolerance));
			Assert.That(target.Mechanical.QDot, Is.EqualTo(33.75f).Within(Tolerance));
		}

		[Test]
		public void UnpoweredImpactDoesNotCreateEnergy()
		{
			var target = CreateTargetState();
			var ball = new BallState {
				Mass = 1f,
				Radius = 25f,
				Velocity = new float3(-30f, 0f, 0f),
			};
			var before = 0.5f * ball.Mass * math.lengthsq(ball.Velocity);

			MechanicalDropTargetPhysics.ResolveImpact(ref ball, ref target.Mechanical,
				in target.Static, new float3(1f, 0f, 0f), 0.35f, 0f);
			var after = 0.5f * ball.Mass * math.lengthsq(ball.Velocity)
				+ 0.5f * target.Static.Mechanical.EffectiveFaceMass
				* target.Mechanical.QDot * target.Mechanical.QDot;

			Assert.That(after, Is.LessThanOrEqualTo(before + Tolerance));
			Assert.That(after, Is.EqualTo(384.1875f).Within(Tolerance));
		}

		[Test]
		public void DampedOscillatorMatchesUndampedQuarterPeriod()
		{
			var frequency = 25f;
			var q = 1f;
			var qDot = 0f;
			var quarterPeriodInternal = 1f / (4f * frequency * PhysicsConstants.DefaultStepTimeS);

			MechanicalDropTargetPhysics.IntegrateDampedOscillator(ref q, ref qDot,
				frequency, 0f, quarterPeriodInternal);

			Assert.That(q, Is.EqualTo(0f).Within(Tolerance));
			Assert.That(qDot, Is.LessThan(0f));
		}

		[Test]
		public void TangentialFrictionReducesSlipAndTransfersSpin()
		{
			var target = CreateTargetState();
			var ball = new BallState {
				Mass = 1f,
				Radius = 25f,
				Velocity = new float3(-30f, 10f, 0f),
			};

			var result = MechanicalDropTargetPhysics.ResolveImpact(ref ball, ref target.Mechanical,
				in target.Static, new float3(1f, 0f, 0f), 0.35f, 0.2f);

			Assert.That(result.Applied, Is.True);
			Assert.That(math.abs(result.TangentImpulse), Is.GreaterThan(0f));
			Assert.That(math.abs(ball.Velocity.y), Is.LessThan(10f));
			Assert.That(math.length(ball.AngularMomentum), Is.GreaterThan(0f));
		}

		[Test]
		public void SweptRearStopReflectsOnlyTheClosingVelocity()
		{
			var config = DropTargetMechanicalConfig.Default;
			config.RearSpringFrequencyHz = 0f;
			config.RearStopTravel = 4f;
			config.RearStopRestitution = 0.5f;
			var mechanical = new DropTargetMechanicalState { Q = 3.9f, QDot = 10f };

			MechanicalDropTargetPhysics.IntegrateRearWithStop(ref mechanical, in config, 0.1f);

			Assert.That(mechanical.Q, Is.LessThanOrEqualTo(config.RearStopTravel));
			Assert.That(mechanical.Q, Is.EqualTo(3.55f).Within(0.01f));
			Assert.That(mechanical.QDot, Is.EqualTo(-5f).Within(Tolerance));
		}

		[Test]
		public void LatchedRestStateStaysExactlyQuiescent()
		{
			var target = CreateTargetState();
			var state = new PhysicsState();

			MechanicalDropTargetPhysics.Step(1, ref target, float3.zero,
				PhysicsConstants.PhysFactor, ref state);

			Assert.That(target.Mechanical.Q, Is.EqualTo(0f));
			Assert.That(target.Mechanical.QDot, Is.EqualTo(0f));
			Assert.That(target.Mechanical.D, Is.EqualTo(0f));
			Assert.That(target.Mechanical.DDot, Is.EqualTo(0f));
		}

		[Test]
		public void EscapeWinsRelatchAtSameStep()
		{
			var target = CreateTargetState();
			target.Mechanical.State = DropTargetMechanismState.Released;
			target.Mechanical.Q = target.Static.Mechanical.LatchRelatchTravel;
			target.Mechanical.QDot = -0.01f;
			target.Mechanical.D = target.Static.Mechanical.LatchEscapeDrop;
			var state = new PhysicsState();

			MechanicalDropTargetPhysics.Step(1, ref target, float3.zero, 0f, ref state);

			Assert.That(target.Mechanical.State, Is.EqualTo(DropTargetMechanismState.Dropping));
		}

		[Test]
		public void ReturnBeforeEscapeRelatchesAsBrick()
		{
			var target = CreateTargetState();
			target.Mechanical.State = DropTargetMechanismState.Released;
			target.Mechanical.Q = target.Static.Mechanical.LatchRelatchTravel;
			target.Mechanical.QDot = -0.01f;
			target.Mechanical.D = target.Static.Mechanical.LatchEscapeDrop - 0.1f;
			var state = new PhysicsState();

			MechanicalDropTargetPhysics.Step(1, ref target, float3.zero, 0f, ref state);

			Assert.That(target.Mechanical.State, Is.EqualTo(DropTargetMechanismState.Latched));
			Assert.That(target.Mechanical.LastImpactOutcome, Is.EqualTo(DropTargetImpactOutcome.BrickRelatch));
			Assert.That(target.Mechanical.D, Is.EqualTo(0f));
		}

		[Test]
		public void ResetStrokePublishesUpwardSurfaceVelocity()
		{
			var target = CreateTargetState();
			target.Mechanical.State = DropTargetMechanismState.Resetting;
			target.Mechanical.D = target.Static.Mechanical.DropTravel;
			target.Mechanical.ResetStartD = target.Mechanical.D;
			target.Mechanical.DroppedSwitchClosed = true;
			var state = new PhysicsState();

			MechanicalDropTargetPhysics.Step(1, ref target, float3.zero,
				PhysicsConstants.PhysFactor, ref state);

			Assert.That(target.Mechanical.D, Is.LessThan(target.Static.Mechanical.DropTravel));
			Assert.That(target.Mechanical.DDot, Is.LessThan(0f));
			Assert.That(MechanicalDropTargetPhysics.SurfaceVelocity(in target.Static,
				in target.Mechanical).z, Is.GreaterThan(0f));
		}

		[Test]
		public void ResetContactUsesFiniteResetMass()
		{
			var target = CreateTargetState();
			target.Mechanical.State = DropTargetMechanismState.Resetting;
			target.Mechanical.DDot = -10f;
			var ball = new BallState {
				Mass = 1f,
				Radius = 25f,
			};

			var result = MechanicalDropTargetPhysics.ResolveImpact(ref ball, ref target.Mechanical,
				in target.Static, new float3(0f, 0f, 1f), 0f, 0f);

			Assert.That(result.Applied, Is.True);
			Assert.That(ball.Velocity.z, Is.EqualTo(5f).Within(Tolerance));
			Assert.That(target.Mechanical.DDot, Is.EqualTo(-5f).Within(Tolerance));
		}

		[Test]
		public void SustainedResetContactSharesGravitySupportWithResetMass()
		{
			var target = CreateTargetState();
			target.Mechanical.State = DropTargetMechanismState.Resetting;
			var ball = new BallState {
				Mass = 1f,
				Radius = 25f,
			};

			var result = MechanicalDropTargetPhysics.ResolveContact(ref ball,
				ref target.Mechanical, in target.Static, new float3(0f, 0f, 1f),
				new float3(0f, 0f, -1f), PhysicsConstants.PhysFactor, 0f);

			Assert.That(result.Applied, Is.True);
			Assert.That(ball.Velocity.z, Is.EqualTo(0.05f).Within(Tolerance));
			Assert.That(target.Mechanical.DDot, Is.EqualTo(0.05f).Within(Tolerance));
		}

		[Test]
		public void CompletedResetRelatchesAndRearmsEvents()
		{
			var target = CreateTargetState();
			target.Mechanical.State = DropTargetMechanismState.Resetting;
			target.Mechanical.D = target.Static.Mechanical.DropTravel;
			target.Mechanical.ResetStartD = target.Mechanical.D;
			target.Mechanical.DroppedSwitchClosed = true;
			target.Mechanical.HitEventFired = true;
			var state = new PhysicsState();

			for (var i = 0; i < 60; i++) {
				MechanicalDropTargetPhysics.Step(1, ref target, float3.zero,
					PhysicsConstants.PhysFactor, ref state);
			}

			Assert.That(target.Mechanical.State, Is.EqualTo(DropTargetMechanismState.Latched));
			Assert.That(target.Mechanical.Q, Is.EqualTo(0f));
			Assert.That(target.Mechanical.QDot, Is.EqualTo(0f));
			Assert.That(target.Mechanical.D, Is.EqualTo(0f));
			Assert.That(target.Mechanical.DDot, Is.EqualTo(0f));
			Assert.That(target.Mechanical.DroppedSwitchClosed, Is.False);
			Assert.That(target.Mechanical.HitEventFired, Is.False);
		}

		[Test]
		public void SimultaneousTwoBallGroupSharesTargetMomentumSymmetrically()
		{
			var target = CreateTargetState();
			var ball = new BallState {
				Mass = 1f,
				Radius = 25f,
				Velocity = new float3(-30f, 0f, 0f),
			};
			var contacts = new NativeList<MechanicalDropTargetContact>(Allocator.Temp);
			try {
				contacts.Add(new MechanicalDropTargetContact {
					BallId = 1,
					Ball = ball,
					Normal = new float3(1f, 0f, 0f),
					Restitution = 0.35f,
				});
				contacts.Add(new MechanicalDropTargetContact {
					BallId = 2,
					Ball = ball,
					Normal = new float3(1f, 0f, 0f),
					Restitution = 0.35f,
				});

				MechanicalDropTargetPhysics.ResolveImpactGroup(ref contacts,
					ref target.Mechanical, in target.Static);

				Assert.That(contacts[0].Applied, Is.EqualTo(1));
				Assert.That(contacts[1].Applied, Is.EqualTo(1));
				Assert.That(contacts[0].NormalImpulse, Is.GreaterThan(0f));
				Assert.That(contacts[1].NormalImpulse, Is.GreaterThan(0f));
				Assert.That(contacts[0].NormalImpulse,
					Is.EqualTo(contacts[1].NormalImpulse).Within(0.01f));
				Assert.That(contacts[0].Ball.Velocity.x,
					Is.EqualTo(contacts[1].Ball.Velocity.x).Within(0.01f));
				Assert.That(math.isfinite(target.Mechanical.QDot), Is.True);
			} finally {
				contacts.Dispose();
			}
		}

		[Test]
		public void MechanicalTargetPresenceGateIgnoresLegacyTargets()
		{
			var targets = new NativeParallelHashMap<int, DropTargetState>(2, Allocator.Temp);
			try {
				Assert.That(MechanicalDropTargetPhysics.ContainsMechanicalTargets(ref targets), Is.False);

				var legacy = CreateTargetState();
				legacy.Static.PhysicsMode = DropTargetPhysicsMode.Legacy;
				targets.Add(1, legacy);
				Assert.That(MechanicalDropTargetPhysics.ContainsMechanicalTargets(ref targets), Is.False);

				targets.Add(2, CreateTargetState());
				Assert.That(MechanicalDropTargetPhysics.ContainsMechanicalTargets(ref targets), Is.True);
			} finally {
				targets.Dispose();
			}
		}

		[Test]
		public void MechanicalContactCandidatesHaveStableTotalOrder()
		{
			var candidates = new NativeList<PhysicsCycle.MechanicalDropTargetContactCandidate>(
				Allocator.Temp);
			try {
				candidates.Add(new PhysicsCycle.MechanicalDropTargetContactCandidate(
					0.002f, 8, 3, 5, ColliderRole.DropTargetPhysicalFace, true, 2));
				candidates.Add(new PhysicsCycle.MechanicalDropTargetContactCandidate(
					0.001f, 8, 3, 5, ColliderRole.DropTargetPhysicalFace, true, 1));
				candidates.Add(new PhysicsCycle.MechanicalDropTargetContactCandidate(
					0.001f, 8, 3, 5, ColliderRole.DropTargetPhysicalFace, true, 0));
				candidates.Add(new PhysicsCycle.MechanicalDropTargetContactCandidate(
					0.001f, 7, 4, 9, ColliderRole.DropTargetPhysicalFace, false));

				candidates.AsArray().Sort();

				Assert.That(candidates[0].TargetItemId, Is.EqualTo(7));
				Assert.That(candidates[1].ContactIndex, Is.EqualTo(0));
				Assert.That(candidates[2].ContactIndex, Is.EqualTo(1));
				Assert.That(candidates[3].HitTimeKey, Is.GreaterThan(candidates[2].HitTimeKey));
			} finally {
				candidates.Dispose();
			}
		}

		[Test]
		public void ComponentRotationZeroFacesPlayerSide()
		{
			var normal = DropTargetComponent.FaceNormalFromRotation(0f);

			Assert.That(normal.x, Is.EqualTo(0f).Within(Tolerance));
			Assert.That(normal.y, Is.EqualTo(1f).Within(Tolerance));
			Assert.That(normal.z, Is.EqualTo(0f).Within(Tolerance));
		}

		[Test]
		public void FrontImpactUsingComponentNormalDrivesReleaseRearward()
		{
			var target = CreateTargetState();
			target.Static.FaceNormal = DropTargetComponent.FaceNormalFromRotation(0f);
			var ball = new BallState {
				Mass = 1f,
				Radius = 25f,
				Position = new float3(0f, 25f, 0f),
				Velocity = new float3(0f, -30f, 0f),
			};

			var result = MechanicalDropTargetPhysics.ResolveImpact(ref ball, ref target.Mechanical,
				in target.Static, in target.Static.FaceNormal, 0.35f, 0f);

			Assert.That(result.Applied, Is.True);
			Assert.That(target.Mechanical.QDot, Is.GreaterThan(0f));
			var state = new PhysicsState();
			MechanicalDropTargetPhysics.Step(1, ref target, float3.zero,
				PhysicsConstants.PhysFactor, ref state);
			Assert.That(target.Mechanical.State, Is.Not.EqualTo(DropTargetMechanismState.Latched));
			Assert.That(target.Mechanical.D, Is.GreaterThan(0f));
		}

		[Test]
		public void DownStopRestitutionReflectsClosingDropVelocity()
		{
			var target = CreateTargetState();
			var config = target.Static.Mechanical;
			config.DropTravel = 1f;
			config.DownStopRestitution = 0.5f;
			target.Static.Mechanical = config;
			target.Mechanical.State = DropTargetMechanismState.Dropping;
			target.Mechanical.D = 0.9f;
			target.Mechanical.DDot = 2f;
			target.Mechanical.DroppedSwitchClosed = true;
			var state = new PhysicsState();

			MechanicalDropTargetPhysics.Step(1, ref target, float3.zero, 0.1f, ref state);

			Assert.That(target.Mechanical.D, Is.EqualTo(config.DropTravel));
			Assert.That(target.Mechanical.DDot, Is.LessThan(0f));
			Assert.That(target.Mechanical.State, Is.EqualTo(DropTargetMechanismState.Dropping));
		}

		[Test]
		public void MechanicalIsDroppedTracksThePhysicalSwitch()
		{
			var mechanical = new DropTargetMechanicalState {
				State = DropTargetMechanismState.ForcedDrop,
				DroppedSwitchClosed = false,
			};
			Assert.That(DropTargetApi.IsMechanicalDropped(in mechanical), Is.False);

			mechanical.State = DropTargetMechanismState.Resetting;
			mechanical.DroppedSwitchClosed = true;
			Assert.That(DropTargetApi.IsMechanicalDropped(in mechanical), Is.True);

			mechanical.DroppedSwitchClosed = false;
			Assert.That(DropTargetApi.IsMechanicalDropped(in mechanical), Is.False);
		}

		private static DropTargetState CreateTargetState()
		{
			return new DropTargetState(0, new DropTargetStaticState {
				PhysicsMode = DropTargetPhysicsMode.Mechanical,
				FaceNormal = new float3(1f, 0f, 0f),
				Mechanical = DropTargetMechanicalConfig.Default,
			}, default) {
				Mechanical = new DropTargetMechanicalState { State = DropTargetMechanismState.Latched }
			};
		}
	}
}
