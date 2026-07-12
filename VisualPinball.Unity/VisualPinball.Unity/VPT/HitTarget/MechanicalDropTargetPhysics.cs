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
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	internal static class MechanicalDropTargetPhysics
	{
		private const int StopRootIterations = 8;

		internal static float3 SurfaceVelocity(in DropTargetStaticState staticState,
			in DropTargetMechanicalState mechanical)
		{
			return -staticState.FaceNormal * mechanical.QDot + new float3(0f, 0f, -1f) * mechanical.DDot;
		}

		internal static DropTargetImpactResult ResolveImpact(ref BallState ball,
			ref DropTargetMechanicalState mechanical, in DropTargetStaticState staticState,
			in float3 contactNormal, float restitution, float friction)
		{
			var config = staticState.Mechanical;
			var normal = math.normalizesafe(contactNormal);
			var contactOffset = -ball.Radius * normal;
			var deflectionAxis = -math.normalizesafe(staticState.FaceNormal);
			var downAxis = new float3(0f, 0f, -1f);
			var targetVelocity = SurfaceVelocity(in staticState, in mechanical);
			var relativeVelocity = BallState.SurfaceVelocity(in ball, in contactOffset) - targetVelocity;
			var normalVelocity = math.dot(relativeVelocity, normal);
			if (normalVelocity >= -PhysicsConstants.LowNormVel || ball.Mass <= 0f
				|| config.EffectiveFaceMass <= 0f) {
				return default;
			}

			var jq = math.dot(deflectionAxis, normal);
			var dIsFree = mechanical.State == DropTargetMechanismState.Released
				|| mechanical.State == DropTargetMechanismState.Dropping
				|| mechanical.State == DropTargetMechanismState.ForcedDrop;
			var jd = dIsFree ? math.dot(downAxis, normal) : 0f;
			var invQMass = 1f / config.EffectiveFaceMass;
			var invDMass = dIsFree && config.DropMass > 0f ? 1f / config.DropMass : 0f;
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
				- SurfaceVelocity(in staticState, in mechanical);
			var tangentVelocity = postRelativeVelocity - normal * math.dot(postRelativeVelocity, normal);
			var tangentSpeed = math.length(tangentVelocity);
			var tangentImpulse = 0f;
			if (tangentSpeed > 1e-5f && friction > 0f) {
				var tangent = tangentVelocity / tangentSpeed;
				var jtq = math.dot(deflectionAxis, tangent);
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

		internal static bool UpdateAll(ref PhysicsState state, float dt)
		{
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
			return changed;
		}

		internal static void Step(int itemId, ref DropTargetState target, in float3 gravity,
			float dt, ref PhysicsState state)
		{
			ref var mechanical = ref target.Mechanical;
			var config = target.Static.Mechanical;
			if (mechanical.State == DropTargetMechanismState.Down
				|| mechanical.State == DropTargetMechanismState.Resetting) {
				target.Animation.ZOffset = -mechanical.D;
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

		private static void PublishPose(int itemId, ref DropTargetState target, ref PhysicsState state)
		{
			ref var mechanical = ref target.Mechanical;
			var linearVelocity = SurfaceVelocity(in target.Static, in mechanical);
			var pose = mechanical.BaseTransform;
			pose.c3.xyz += -target.Static.FaceNormal * mechanical.Q
				+ new float3(0f, 0f, -1f) * mechanical.D;
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
