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

using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	internal static class MechanicalDropTargetPhysics
	{
		private const int StopRootIterations = 8;
		private const int ContactSolverIterations = 24;
		private const float MillisecondsPerInternalTime = PhysicsConstants.DefaultStepTimeS * 1000f;
		private static readonly ProfilerMarker PerfMarkerUpdate = new("DropTarget.MechanicalUpdate");
		private static readonly ProfilerMarker PerfMarkerImpactGroup = new("DropTarget.ImpactGroup");

		internal static bool ContainsMechanicalTargets(
			ref NativeParallelHashMap<int, DropTargetState> targets)
		{
			using var enumerator = targets.GetEnumerator();
			while (enumerator.MoveNext()) {
				if (enumerator.Current.Value.Static.PhysicsMode == DropTargetPhysicsMode.Mechanical) {
					return true;
				}
			}
			return false;
		}

		internal static float3 SurfaceVelocity(in DropTargetStaticState staticState,
			in DropTargetMechanicalState mechanical)
		{
			var point = mechanical.PoseInitialized ? mechanical.BaseTransform.c3.xyz : staticState.Center;
			return DropTargetDeflectionPhysics.SurfaceVelocityAtPoint(in staticState, in mechanical, in point);
		}

		internal static float3 SurfaceVelocityAtPoint(in DropTargetStaticState staticState,
			in DropTargetMechanicalState mechanical, in float3 point)
		{
			return DropTargetDeflectionPhysics.SurfaceVelocityAtPoint(in staticState, in mechanical, in point);
		}

		internal static DropTargetImpactResult ResolveImpact(ref BallState ball,
			ref DropTargetMechanicalState mechanical, in DropTargetStaticState staticState,
			in float3 contactNormal, float restitution, float friction)
		{
			var config = staticState.Mechanical;
			var normal = math.normalizesafe(contactNormal);
			var contactOffset = -ball.Radius * normal;
			var contactPoint = ball.Position + contactOffset;
			var deflection = DropTargetDeflectionPhysics.AtPoint(in staticState, in mechanical,
				in contactPoint);
			var downAxis = new float3(0f, 0f, -1f);
			var targetVelocity = SurfaceVelocityAtPoint(in staticState, in mechanical, in contactPoint);
			var relativeVelocity = BallState.SurfaceVelocity(in ball, in contactOffset) - targetVelocity;
			var normalVelocity = math.dot(relativeVelocity, normal);
			if (normalVelocity >= -PhysicsConstants.LowNormVel || ball.Mass <= 0f
				|| config.EffectiveFaceMass <= 0f) {
				return default;
			}

			var jq = math.dot(deflection.VelocityJacobian, normal);
			var isResetStroke = mechanical.State == DropTargetMechanismState.Resetting
				|| mechanical.State == DropTargetMechanismState.Settling;
			var dIsFree = mechanical.State == DropTargetMechanismState.Released
				|| mechanical.State == DropTargetMechanismState.Dropping
				|| mechanical.State == DropTargetMechanismState.ForcedDrop
				|| isResetStroke;
			var jd = dIsFree ? math.dot(downAxis, normal) : 0f;
			var invQMass = deflection.InverseGeneralizedMass;
			var dMass = isResetStroke ? config.ResetEffectiveMass : config.DropMass;
			var invDMass = dIsFree && dMass > 0f ? 1f / dMass : 0f;
			var ballAngularTerm = math.dot(normal,
				math.cross(math.cross(contactOffset, normal) / ball.Inertia, contactOffset));
			var normalMass = ball.InvMass + ballAngularTerm + jq * jq * invQMass + jd * jd * invDMass;
			if (normalMass <= 0f) {
				return default;
			}

			var normalImpulse = -(1f + math.clamp(restitution, 0f, 1f)) * normalVelocity / normalMass;
			var impulse = normal * normalImpulse;
			ball.ApplySurfaceImpulse(math.cross(contactOffset, impulse), impulse);
			mechanical.QDot -= jq * normalImpulse * invQMass;
			mechanical.DDot -= jd * normalImpulse * invDMass;

			var postRelativeVelocity = BallState.SurfaceVelocity(in ball, in contactOffset)
				- SurfaceVelocityAtPoint(in staticState, in mechanical, in contactPoint);
			var tangentVelocity = postRelativeVelocity - normal * math.dot(postRelativeVelocity, normal);
			var tangentSpeed = math.length(tangentVelocity);
			var tangentImpulse = 0f;
			if (tangentSpeed > 1e-5f && friction > 0f) {
				var tangent = tangentVelocity / tangentSpeed;
				var jtq = math.dot(deflection.VelocityJacobian, tangent);
				var jtd = dIsFree ? math.dot(downAxis, tangent) : 0f;
				var ballTangentTerm = math.dot(tangent,
					math.cross(math.cross(contactOffset, tangent) / ball.Inertia, contactOffset));
				var tangentMass = ball.InvMass + ballTangentTerm + jtq * jtq * invQMass
					+ jtd * jtd * invDMass;
				if (tangentMass > 0f) {
					tangentImpulse = math.clamp(-tangentSpeed / tangentMass,
						-friction * normalImpulse, friction * normalImpulse);
					var frictionImpulse = tangent * tangentImpulse;
					ball.ApplySurfaceImpulse(math.cross(contactOffset, frictionImpulse), frictionImpulse);
					mechanical.QDot -= jtq * tangentImpulse * invQMass;
					mechanical.DDot -= jtd * tangentImpulse * invDMass;
				}
			}

			return new DropTargetImpactResult(true, normalImpulse, tangentImpulse);
		}

		internal static void ResolveImpactGroup(ref NativeList<MechanicalDropTargetContact> contacts,
			ref DropTargetMechanicalState mechanical, in DropTargetStaticState staticState)
		{
			if (contacts.Length == 0) {
				return;
			}

			var config = staticState.Mechanical;
			if (config.EffectiveFaceMass <= 0f) {
				return;
			}
			PerfMarkerImpactGroup.Begin();
			var downAxis = new float3(0f, 0f, -1f);
			var isResetStroke = mechanical.State == DropTargetMechanismState.Resetting
				|| mechanical.State == DropTargetMechanismState.Settling;
			var dIsFree = mechanical.State == DropTargetMechanismState.Released
				|| mechanical.State == DropTargetMechanismState.Dropping
				|| mechanical.State == DropTargetMechanismState.ForcedDrop
				|| isResetStroke;
			var dMass = isResetStroke ? config.ResetEffectiveMass : config.DropMass;
			var invDMass = dIsFree && dMass > 0f ? 1f / dMass : 0f;

			for (var i = 0; i < contacts.Length; i++) {
				ref var contact = ref contacts.GetElementAsRef(i);
				contact.Normal = math.normalizesafe(contact.Normal);
				var contactOffset = -contact.Ball.Radius * contact.Normal;
				var contactPoint = contact.Ball.Position + contactOffset;
				var relativeVelocity = BallState.SurfaceVelocity(in contact.Ball, in contactOffset)
					- SurfaceVelocityAtPoint(in staticState, in mechanical, in contactPoint);
				var normalVelocity = math.dot(relativeVelocity, contact.Normal);
				var tangentVelocity = relativeVelocity - contact.Normal * normalVelocity;
				var tangentSpeed = math.length(tangentVelocity);
				contact.ApproachSpeed = -normalVelocity;
				contact.Applied = normalVelocity < -PhysicsConstants.LowNormVel
					&& contact.Ball.Mass > 0f ? (byte)1 : (byte)0;
				contact.HasTangent = tangentSpeed > 1e-5f ? (byte)1 : (byte)0;
				contact.Tangent = contact.HasTangent != 0 ? tangentVelocity / tangentSpeed : float3.zero;
				contact.NormalImpulse = 0f;
				contact.TangentImpulse = 0f;
			}

			for (var iteration = 0; iteration < ContactSolverIterations; iteration++) {
				for (var i = 0; i < contacts.Length; i++) {
					ref var contact = ref contacts.GetElementAsRef(i);
					if (contact.Applied == 0) {
						continue;
					}
					var normal = contact.Normal;
					var contactOffset = -contact.Ball.Radius * normal;
					var contactPoint = contact.Ball.Position + contactOffset;
					var deflection = DropTargetDeflectionPhysics.AtPoint(in staticState,
						in mechanical, in contactPoint);
					var jq = math.dot(deflection.VelocityJacobian, normal);
					var jd = dIsFree ? math.dot(downAxis, normal) : 0f;
					var ballAngularTerm = math.dot(normal,
						math.cross(math.cross(contactOffset, normal) / contact.Ball.Inertia, contactOffset));
					var effectiveInvMass = contact.Ball.InvMass + ballAngularTerm
						+ jq * jq * deflection.InverseGeneralizedMass + jd * jd * invDMass;
					if (effectiveInvMass <= 0f) {
						continue;
					}

					var relativeVelocity = BallState.SurfaceVelocity(in contact.Ball, in contactOffset)
						- SurfaceVelocityAtPoint(in staticState, in mechanical, in contactPoint);
					var desiredVelocity = math.clamp(contact.Restitution, 0f, 1f) * contact.ApproachSpeed;
					var impulseDelta = (desiredVelocity - math.dot(relativeVelocity, normal))
						/ effectiveInvMass;
					var accumulatedImpulse = math.max(contact.NormalImpulse + impulseDelta, 0f);
					impulseDelta = accumulatedImpulse - contact.NormalImpulse;
					contact.NormalImpulse = accumulatedImpulse;
					ApplyGroupImpulse(ref contact.Ball, ref mechanical,
						in deflection.VelocityJacobian, in downAxis, in normal, in contactOffset,
						impulseDelta, dIsFree, deflection.InverseGeneralizedMass, invDMass);
				}
			}

			for (var iteration = 0; iteration < ContactSolverIterations; iteration++) {
				for (var i = 0; i < contacts.Length; i++) {
					ref var contact = ref contacts.GetElementAsRef(i);
					if (contact.Applied == 0 || contact.HasTangent == 0
						|| contact.Friction <= 0f || contact.NormalImpulse <= 0f) {
						continue;
					}
					var normal = contact.Normal;
					var contactOffset = -contact.Ball.Radius * normal;
					var contactPoint = contact.Ball.Position + contactOffset;
					var deflection = DropTargetDeflectionPhysics.AtPoint(in staticState,
						in mechanical, in contactPoint);
					var relativeVelocity = BallState.SurfaceVelocity(in contact.Ball, in contactOffset)
						- SurfaceVelocityAtPoint(in staticState, in mechanical, in contactPoint);
					var tangent = contact.Tangent;
					var jq = math.dot(deflection.VelocityJacobian, tangent);
					var jd = dIsFree ? math.dot(downAxis, tangent) : 0f;
					var ballAngularTerm = math.dot(tangent,
						math.cross(math.cross(contactOffset, tangent) / contact.Ball.Inertia, contactOffset));
					var effectiveInvMass = contact.Ball.InvMass + ballAngularTerm
						+ jq * jq * deflection.InverseGeneralizedMass + jd * jd * invDMass;
					if (effectiveInvMass <= 0f) {
						continue;
					}

					var impulseDelta = -math.dot(relativeVelocity, tangent) / effectiveInvMass;
					var limit = contact.Friction * contact.NormalImpulse;
					var accumulatedImpulse = math.clamp(contact.TangentImpulse + impulseDelta, -limit, limit);
					impulseDelta = accumulatedImpulse - contact.TangentImpulse;
					contact.TangentImpulse = accumulatedImpulse;
					ApplyGroupImpulse(ref contact.Ball, ref mechanical,
						in deflection.VelocityJacobian, in downAxis, in tangent, in contactOffset,
						impulseDelta, dIsFree, deflection.InverseGeneralizedMass, invDMass);
				}
			}
			PerfMarkerImpactGroup.End();
		}

		internal static DropTargetImpactResult ResolveContact(ref BallState ball,
			ref DropTargetMechanicalState mechanical, in DropTargetStaticState staticState,
			in float3 contactNormal, in float3 gravity, float dt, float friction)
		{
			var config = staticState.Mechanical;
			var normal = math.normalizesafe(contactNormal);
			var contactOffset = -ball.Radius * normal;
			var contactPoint = ball.Position + contactOffset;
			var deflection = DropTargetDeflectionPhysics.AtPoint(in staticState, in mechanical,
				in contactPoint);
			var downAxis = new float3(0f, 0f, -1f);
			var dMass = config.ResetEffectiveMass;
			if (ball.Mass <= 0f || config.EffectiveFaceMass <= 0f || dMass <= 0f) {
				return default;
			}

			var jq = math.dot(deflection.VelocityJacobian, normal);
			var jd = math.dot(downAxis, normal);
			var invQMass = deflection.InverseGeneralizedMass;
			var invDMass = 1f / dMass;
			var ballAngularTerm = math.dot(normal,
				math.cross(math.cross(contactOffset, normal) / ball.Inertia, contactOffset));
			var effectiveInvMass = ball.InvMass + ballAngularTerm
				+ jq * jq * invQMass + jd * jd * invDMass;
			if (effectiveInvMass <= 0f) {
				return default;
			}

			var relativeVelocity = BallState.SurfaceVelocity(in ball, in contactOffset)
				- SurfaceVelocityAtPoint(in staticState, in mechanical, in contactPoint);
			var closingVelocity = math.dot(relativeVelocity, normal)
				+ math.dot(gravity, normal) * math.max(dt, 0f);
			var normalImpulse = math.max(-closingVelocity / effectiveInvMass, 0f);
			if (normalImpulse <= 0f) {
				return default;
			}
			ApplyGroupImpulse(ref ball, ref mechanical, in deflection.VelocityJacobian, in downAxis,
				in normal, in contactOffset, normalImpulse, true, invQMass, invDMass);

			var postRelativeVelocity = BallState.SurfaceVelocity(in ball, in contactOffset)
				- SurfaceVelocityAtPoint(in staticState, in mechanical, in contactPoint);
			var tangentVelocity = postRelativeVelocity - normal * math.dot(postRelativeVelocity, normal);
			var tangentSpeed = math.length(tangentVelocity);
			var tangentImpulse = 0f;
			if (tangentSpeed > 1e-5f && friction > 0f) {
				var tangent = tangentVelocity / tangentSpeed;
				var jtq = math.dot(deflection.VelocityJacobian, tangent);
				var jtd = math.dot(downAxis, tangent);
				var tangentAngularTerm = math.dot(tangent,
					math.cross(math.cross(contactOffset, tangent) / ball.Inertia, contactOffset));
				var tangentInvMass = ball.InvMass + tangentAngularTerm
					+ jtq * jtq * invQMass + jtd * jtd * invDMass;
				if (tangentInvMass > 0f) {
					tangentImpulse = math.clamp(-tangentSpeed / tangentInvMass,
						-friction * normalImpulse, friction * normalImpulse);
					ApplyGroupImpulse(ref ball, ref mechanical, in deflection.VelocityJacobian, in downAxis,
						in tangent, in contactOffset, tangentImpulse, true, invQMass, invDMass);
				}
			}

			return new DropTargetImpactResult(true, normalImpulse, tangentImpulse);
		}

		private static void ApplyGroupImpulse(ref BallState ball,
			ref DropTargetMechanicalState mechanical, in float3 deflectionVelocityJacobian, in float3 downAxis,
			in float3 direction, in float3 contactOffset, float impulseMagnitude, bool dIsFree,
			float invQMass, float invDMass)
		{
			if (impulseMagnitude == 0f) {
				return;
			}
			var impulse = direction * impulseMagnitude;
			ball.ApplySurfaceImpulse(math.cross(contactOffset, impulse), impulse);
			mechanical.QDot -= math.dot(deflectionVelocityJacobian, direction) * impulseMagnitude * invQMass;
			if (dIsFree) {
				mechanical.DDot -= math.dot(downAxis, direction) * impulseMagnitude * invDMass;
			}
		}

		internal static bool UpdateAll(ref PhysicsState state, float dt)
		{
			PerfMarkerUpdate.Begin();
			var changed = false;
			using var enumerator = state.DropTargetStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				var itemId = enumerator.Current.Key;
				ref var target = ref enumerator.Current.Value;
				if (target.Static.PhysicsMode != DropTargetPhysicsMode.Mechanical) {
					continue;
				}
				ref var mechanical = ref target.Mechanical;
				if (!mechanical.PoseInitialized) {
					if (!state.KinematicTransforms.TryGetValue(itemId, out mechanical.BaseTransform)) {
						continue;
					}
					mechanical.PoseInitialized = true;
				}
				if (mechanical.State == DropTargetMechanismState.Down) {
					continue;
				}
				if (mechanical.State == DropTargetMechanismState.Latched
					&& mechanical.Q == 0f && mechanical.QDot == 0f
					&& mechanical.D == 0f && mechanical.DDot == 0f) {
					continue;
				}

				var previousQ = mechanical.Q;
				var previousD = mechanical.D;
				Step(itemId, ref target, in state.Env.Gravity, dt, ref state);
				if (math.abs(mechanical.Q - previousQ) <= 1e-6f
					&& math.abs(mechanical.D - previousD) <= 1e-6f) {
					continue;
				}
				PublishPose(itemId, ref target, ref state);
				changed = true;
			}
			PerfMarkerUpdate.End();
			return changed;
		}

		internal static void Step(int itemId, ref DropTargetState target, in float3 gravity,
			float dt, ref PhysicsState state)
		{
			ref var mechanical = ref target.Mechanical;
			var config = target.Static.Mechanical;
			if (mechanical.State == DropTargetMechanismState.Down) {
				target.Animation.ZOffset = -mechanical.D;
				return;
			}
			if (mechanical.State == DropTargetMechanismState.Resetting) {
				IntegrateReset(itemId, ref target, dt, ref state);
				return;
			}
			if (mechanical.State == DropTargetMechanismState.Settling) {
				IntegrateSettle(itemId, ref target, dt, ref state);
				return;
			}

			IntegrateRearWithStop(ref mechanical, in config, dt);
			if (mechanical.State == DropTargetMechanismState.Latched
				&& mechanical.Q >= config.LatchReleaseTravel) {
				mechanical.State = DropTargetMechanismState.Released;
				mechanical.LastImpactOutcome = DropTargetImpactOutcome.FaceDrop;
			}

			if (mechanical.State == DropTargetMechanismState.Released
				|| mechanical.State == DropTargetMechanismState.Dropping
				|| mechanical.State == DropTargetMechanismState.ForcedDrop) {
				IntegrateDrop(ref mechanical, in config, in gravity, dt);
				// Escape wins a same-tick relatch boundary.
				if (mechanical.D >= config.LatchEscapeDrop) {
					mechanical.State = DropTargetMechanismState.Dropping;
				} else if (mechanical.Q <= config.LatchRelatchTravel && mechanical.QDot < 0f) {
					mechanical.State = DropTargetMechanismState.Latched;
					mechanical.LastImpactOutcome = DropTargetImpactOutcome.BrickRelatch;
					mechanical.D = 0f;
					mechanical.DDot = 0f;
					mechanical.HitEventFired = false;
				}
			}

			if (!mechanical.DroppedSwitchClosed && mechanical.D >= config.DroppedSwitchTravel) {
				mechanical.DroppedSwitchClosed = true;
				if (target.Static.UseHitEvent) {
					state.EventQueue.Enqueue(new EventData(EventId.TargetEventsDropped, itemId));
				}
			}
			if (mechanical.D >= config.DropTravel) {
				mechanical.D = config.DropTravel;
				mechanical.DDot = 0f;
				mechanical.State = DropTargetMechanismState.Down;
				target.Animation.IsDropped = true;
				state.DisableColliders(itemId);
			}
			if (mechanical.State == DropTargetMechanismState.Latched
				&& math.abs(mechanical.Q) < 1e-4f && math.abs(mechanical.QDot) < 1e-4f) {
				mechanical.Q = 0f;
				mechanical.QDot = 0f;
				mechanical.HitEventFired = false;
			}
			target.Animation.ZOffset = -mechanical.D;
		}

		internal static void BeginReset(int itemId, ref DropTargetState target, ref PhysicsState state)
		{
			ref var mechanical = ref target.Mechanical;
			if (mechanical.State == DropTargetMechanismState.Latched) {
				return;
			}

			mechanical.State = DropTargetMechanismState.Resetting;
			mechanical.ResetStartD = mechanical.D;
			mechanical.ResetElapsedMs = 0f;
			mechanical.SettleElapsedMs = 0f;
			mechanical.Q = 0f;
			mechanical.QDot = 0f;
			mechanical.DDot = 0f;
			target.Animation.IsDropped = false;
			target.Animation.MoveAnimation = false;
			target.Animation.HitEvent = false;
			state.EnableColliders(itemId);
		}

		internal static void ForceDrop(int itemId, ref DropTargetState target, ref PhysicsState state)
		{
			ref var mechanical = ref target.Mechanical;
			if (mechanical.State == DropTargetMechanismState.Down) {
				return;
			}

			mechanical.State = DropTargetMechanismState.ForcedDrop;
			mechanical.LastImpactOutcome = DropTargetImpactOutcome.ForcedDrop;
			mechanical.ResetElapsedMs = 0f;
			mechanical.SettleElapsedMs = 0f;
			mechanical.D = math.max(mechanical.D, 0f);
			mechanical.DDot = math.max(mechanical.DDot, 0f);
			target.Animation.IsDropped = false;
			target.Animation.MoveAnimation = false;
			target.Animation.HitEvent = false;
			state.EnableColliders(itemId);
		}

		internal static void IntegrateRearWithStop(ref DropTargetMechanicalState state,
			in DropTargetMechanicalConfig config, float dt)
		{
			var startQ = state.Q;
			var startQDot = state.QDot;
			IntegrateDampedOscillator(ref state.Q, ref state.QDot, config.RearSpringFrequencyHz,
				config.RearDampingRatio, dt);
			if (state.Q <= config.RearStopTravel || config.RearStopTravel <= 0f) {
				return;
			}

			var low = 0f;
			var high = dt;
			for (var i = 0; i < StopRootIterations; i++) {
				var mid = (low + high) * 0.5f;
				var q = startQ;
				var qDot = startQDot;
				IntegrateDampedOscillator(ref q, ref qDot, config.RearSpringFrequencyHz,
					config.RearDampingRatio, mid);
				if (q < config.RearStopTravel) {
					low = mid;
				} else {
					high = mid;
				}
			}
			state.Q = startQ;
			state.QDot = startQDot;
			IntegrateDampedOscillator(ref state.Q, ref state.QDot, config.RearSpringFrequencyHz,
				config.RearDampingRatio, high);
			state.Q = config.RearStopTravel;
			if (state.QDot > 0f) {
				state.QDot = -state.QDot * math.clamp(config.RearStopRestitution, 0f, 1f);
			}
			IntegrateDampedOscillator(ref state.Q, ref state.QDot, config.RearSpringFrequencyHz,
				config.RearDampingRatio, dt - high);
		}

		internal static void IntegrateDampedOscillator(ref float q, ref float qDot,
			float frequencyHz, float dampingRatio, float dt)
		{
			var omega = 2f * math.PI * math.max(frequencyHz, 0f) * PhysicsConstants.DefaultStepTimeS;
			if (omega <= 0f || dt <= 0f) {
				q += qDot * dt;
				return;
			}
			var zeta = math.max(dampingRatio, 0f);
			if (zeta < 1f - 1e-4f) {
				var wd = omega * math.sqrt(1f - zeta * zeta);
				var a = q;
				var b = (qDot + zeta * omega * q) / wd;
				var sin = math.sin(wd * dt);
				var cos = math.cos(wd * dt);
				var decay = math.exp(-zeta * omega * dt);
				var wave = a * cos + b * sin;
				q = decay * wave;
				qDot = decay * (-zeta * omega * wave - a * wd * sin + b * wd * cos);
			} else if (zeta <= 1f + 1e-4f) {
				var a = q;
				var b = qDot + omega * q;
				var decay = math.exp(-omega * dt);
				q = decay * (a + b * dt);
				qDot = decay * (b - omega * (a + b * dt));
			} else {
				var root = math.sqrt(zeta * zeta - 1f);
				var r1 = -omega * (zeta - root);
				var r2 = -omega * (zeta + root);
				var c1 = (qDot - r2 * q) / (r1 - r2);
				var c2 = q - c1;
				var e1 = math.exp(r1 * dt);
				var e2 = math.exp(r2 * dt);
				q = c1 * e1 + c2 * e2;
				qDot = c1 * r1 * e1 + c2 * r2 * e2;
			}
		}

		private static void IntegrateDrop(ref DropTargetMechanicalState state,
			in DropTargetMechanicalConfig config, in float3 gravity, float dt)
		{
			if (config.DropMass <= 0f) {
				return;
			}
			var downAxis = new float3(0f, 0f, -1f);
			var drive = config.DropSpringForce - config.GuideDamping * state.DDot;
			if (math.abs(state.DDot) <= config.GuideVelocityDeadband) {
				if (math.abs(drive) <= config.GuideFriction) {
					drive = 0f;
					state.DDot = 0f;
				} else {
					drive -= math.sign(drive) * config.GuideFriction;
				}
			} else {
				drive -= math.sign(state.DDot) * config.GuideFriction;
			}
			var acceleration = math.dot(gravity, downAxis) + drive / config.DropMass;
			state.DDot += acceleration * dt;
			state.D = math.max(0f, state.D + state.DDot * dt);
		}

		private static void IntegrateReset(int itemId, ref DropTargetState target, float dt,
			ref PhysicsState state)
		{
			// The reset bar is a prescribed, powered actuator. Contact sees its finite effective
			// mass during the current solve, but the next step resumes trajectory tracking; the
			// actuator supplies the momentum needed to stay on that measured motion.
			ref var mechanical = ref target.Mechanical;
			var config = target.Static.Mechanical;
			var previousD = mechanical.D;
			mechanical.ResetElapsedMs += math.max(dt, 0f) * MillisecondsPerInternalTime;
			var progress = config.ResetDurationMs <= 0f
				? 1f
				: math.saturate(mechanical.ResetElapsedMs / config.ResetDurationMs);
			var smoothProgress = MinimumJerk(progress);
			mechanical.D = math.lerp(mechanical.ResetStartD, -math.max(config.ResetOvershootTravel, 0f),
				smoothProgress);
			mechanical.DDot = dt > 0f ? (mechanical.D - previousD) / dt : 0f;
			FireRaisedCrossing(itemId, ref target, ref state);

			if (progress >= 1f) {
				mechanical.State = DropTargetMechanismState.Settling;
				mechanical.SettleElapsedMs = 0f;
			}
			target.Animation.ZOffset = -mechanical.D;
		}

		private static void IntegrateSettle(int itemId, ref DropTargetState target, float dt,
			ref PhysicsState state)
		{
			// Settling remains position-driven for the same powered-actuator reason as reset.
			ref var mechanical = ref target.Mechanical;
			var config = target.Static.Mechanical;
			var previousD = mechanical.D;
			mechanical.SettleElapsedMs += math.max(dt, 0f) * MillisecondsPerInternalTime;
			var progress = config.ResetSettleDelayMs <= 0f
				? 1f
				: math.saturate(mechanical.SettleElapsedMs / config.ResetSettleDelayMs);
			mechanical.D = math.lerp(-math.max(config.ResetOvershootTravel, 0f), 0f,
				MinimumJerk(progress));
			mechanical.DDot = dt > 0f ? (mechanical.D - previousD) / dt : 0f;
			FireRaisedCrossing(itemId, ref target, ref state);

			if (progress >= 1f) {
				mechanical.State = DropTargetMechanismState.Latched;
				mechanical.Q = 0f;
				mechanical.QDot = 0f;
				mechanical.D = 0f;
				mechanical.DDot = 0f;
				mechanical.ResetStartD = 0f;
				mechanical.ResetElapsedMs = 0f;
				mechanical.SettleElapsedMs = 0f;
				mechanical.DroppedSwitchClosed = false;
				mechanical.HitEventFired = false;
				target.Animation.IsDropped = false;
				target.Animation.MoveAnimation = false;
			}
			target.Animation.ZOffset = -mechanical.D;
		}

		private static void FireRaisedCrossing(int itemId, ref DropTargetState target,
			ref PhysicsState state)
		{
			ref var mechanical = ref target.Mechanical;
			if (!mechanical.DroppedSwitchClosed
				|| mechanical.D > target.Static.Mechanical.RaisedSwitchTravel) {
				return;
			}

			mechanical.DroppedSwitchClosed = false;
			if (target.Static.UseHitEvent) {
				state.EventQueue.Enqueue(new EventData(EventId.TargetEventsRaised, itemId));
			}
		}

		private static float MinimumJerk(float t)
		{
			return t * t * t * (10f + t * (-15f + 6f * t));
		}

		private static void PublishPose(int itemId, ref DropTargetState target, ref PhysicsState state)
		{
			ref var mechanical = ref target.Mechanical;
			var pose = mechanical.BaseTransform;
			if (target.Static.Mechanical.DeflectionKind == DropTargetDeflectionKind.HingedBlade) {
				var basePosition = pose.c3.xyz;
				var deflection = DropTargetDeflectionPhysics.AtPoint(in target.Static,
					in mechanical, in basePosition);
				if (deflection.InverseGeneralizedMass > 0f) {
					var rotateAroundPivot = math.mul(
						float4x4.TRS(deflection.Pivot, quaternion.AxisAngle(deflection.Axis, mechanical.Q),
							new float3(1f)),
						float4x4.Translate(-deflection.Pivot));
					pose = math.mul(rotateAroundPivot, pose);
				}
			} else {
				pose.c3.xyz += -target.Static.FaceNormal * mechanical.Q;
			}
			pose.c3.xyz += new float3(0f, 0f, -1f) * mechanical.D;
			var posePosition = pose.c3.xyz;
			var linearVelocity = SurfaceVelocityAtPoint(in target.Static, in mechanical, in posePosition);
			state.KinematicTransforms[itemId] = pose;
			var velocityState = new KinematicVelocityState {
				LinearVelocity = linearVelocity,
				StepVelocity = linearVelocity,
				Pivot = pose.c3.xyz,
				LastUpdateUsec = state.Env.CurPhysicsFrameTime,
			};
			if (!state.KinematicVelocities.TryAdd(itemId, velocityState)) {
				state.KinematicVelocities[itemId] = velocityState;
			}
			if (!state.KinematicColliderLookups.TryGetValue(itemId, out var colliderLookups)) {
				return;
			}
			for (var i = 0; i < colliderLookups.Length; i++) {
				state.TransformKinematicColliders(colliderLookups[i], pose);
			}
		}
	}
}
