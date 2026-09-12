// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Once-per-tick reciprocal coupling between Spatial Physical magnets and
	/// their spring-hinge owners. Candidate selection precedes every field or
	/// hold impulse, and each hinge velocity is committed at most once.
	/// </summary>
	internal static class OwnedMagnetPhysics
	{
		private const float MinimumValue = 1e-6f;
		private const float ReleaseGapMultiplier = 1.5f;
		private const byte SaturationReleaseTicks = 3;

		internal static void Update(ref PhysicsState state, float step)
		{
			PrepareAttachmentsAndCandidates(ref state);
			ResolveCandidateConflicts(ref state);
			AcquireCandidates(ref state);
			ApplyOwnedFields(ref state, step);
			CommitOwnedHolds(ref state, step);
		}

		private static void PrepareAttachmentsAndCandidates(ref PhysicsState state)
		{
			using var magnets = state.MagnetStates.GetEnumerator();
			while (magnets.MoveNext()) {
				var itemId = magnets.Current.Key;
				ref var magnet = ref magnets.Current.Value;
				magnet.CandidateBallId = 0;
				magnet.CandidateDistanceSq = float.MaxValue;
				if (!magnet.CoupleToHinge) {
					continue;
				}
				if (!IsUsableOwner(in magnet, ref state) || !IsPrimaryForOwner(itemId, in magnet, ref state)) {
					ReleaseAttachment(itemId, ref magnet, ref state, true);
					magnet.ReleasedBalls = default;
					ClearMemberships(itemId, ref state);
					continue;
				}

				ref var hinge = ref state.SpringHingeStates.GetValueByRef(magnet.HingeOwnerId);
				GetPose(in magnet, in hinge, out var pole, out var target);
				magnet.Position = pole.xy;
				magnet.Height = pole.z;

				if (magnet.AttachedBallId != 0) {
					if (!state.Balls.ContainsKey(magnet.AttachedBallId)) {
						magnet.AttachedBallId = 0;
						magnet.SaturationTicks = 0;
					} else {
						ref var attached = ref state.Balls.GetValueByRef(magnet.AttachedBallId);
						var releaseDistance = math.max(magnet.GrabRadius * ReleaseGapMultiplier,
							attached.Radius + PhysicsConstants.PhysTouch);
						if (attached.AttachedMagnetId != itemId || attached.IsFrozen || attached.ManualControl
						    || !MagnetPhysics.HasActiveField(in magnet)
						    || math.distancesq(attached.Position, target) > releaseDistance * releaseDistance
						    || !HasValidProxyGap(in attached, in hinge, in target, ref state)
						    || !CanCaptureWithin(in attached, in magnet, in hinge, in pole, in target,
							    releaseDistance)) {
							ReleaseAttachment(itemId, ref magnet, ref state, true);
						}
					}
				}
				if (!MagnetPhysics.HasActiveField(in magnet)) {
					magnet.ReleasedBalls = default;
				}
				UpdateMemberships(itemId, ref magnet, in pole, in target, ref state);

				if (magnet.AttachedBallId != 0 || !MagnetPhysics.HasActiveField(in magnet)
				    || magnet.GrabRadius <= 0f || magnet.MaxHoldForce <= 0f) {
					continue;
				}

				using var balls = state.Balls.GetEnumerator();
				while (balls.MoveNext()) {
					ref var ball = ref balls.Current.Value;
					if (ball.IsFrozen || ball.ManualControl || ball.AttachedMagnetId != 0
					    || IsLegacyGrabbed(ball.Id, ref state)) {
						continue;
					}
					var distanceSq = math.distancesq(ball.Position, target);
					if (state.InsideOfs.TryGetBitIndex(ball.Id, out var bitIndex)
					    && magnet.ReleasedBalls.IsSet(bitIndex)) {
						if (distanceSq <= magnet.GrabRadius * magnet.GrabRadius) {
							continue;
						}
						magnet.ReleasedBalls.SetBits(bitIndex, false);
					}
					if (distanceSq > magnet.GrabRadius * magnet.GrabRadius
					    || !HasValidProxyGap(in ball, in hinge, in target, ref state)
					    || !CanCapture(in ball, in magnet, in hinge, in pole, in target)) {
						continue;
					}
					if (distanceSq < magnet.CandidateDistanceSq
					    || distanceSq == magnet.CandidateDistanceSq && ball.Id < magnet.CandidateBallId) {
						magnet.CandidateBallId = ball.Id;
						magnet.CandidateDistanceSq = distanceSq;
					}
				}
			}
		}

		private static void ClearMemberships(int itemId, ref PhysicsState state)
		{
			using var balls = state.Balls.GetEnumerator();
			while (balls.MoveNext()) {
				MagnetPhysics.UpdateMembership(itemId, balls.Current.Key, false, ref state);
			}
		}

		private static void UpdateMemberships(int itemId, ref MagnetState magnet, in float3 pole,
			in float3 target, ref PhysicsState state)
		{
			var hasField = MagnetPhysics.HasActiveField(in magnet);
			var radiusSq = magnet.Radius * magnet.Radius;
			using var balls = state.Balls.GetEnumerator();
			while (balls.MoveNext()) {
				ref var ball = ref balls.Current.Value;
				if (state.InsideOfs.TryGetBitIndex(ball.Id, out var bitIndex)
				    && magnet.ReleasedBalls.IsSet(bitIndex)
				    && math.distancesq(ball.Position, target) > magnet.GrabRadius * magnet.GrabRadius) {
					magnet.ReleasedBalls.SetBits(bitIndex, false);
				}
				var isInside = hasField && !ball.IsFrozen
					&& (ball.Id == magnet.AttachedBallId
					    || radiusSq > MinimumValue && math.distancesq(ball.Position, pole) < radiusSq);
				MagnetPhysics.UpdateMembership(itemId, ball.Id, isInside, ref state);
			}
		}

		private static void ResolveCandidateConflicts(ref PhysicsState state)
		{
			using var magnets = state.MagnetStates.GetEnumerator();
			while (magnets.MoveNext()) {
				var itemId = magnets.Current.Key;
				ref var magnet = ref magnets.Current.Value;
				if (magnet.CandidateBallId == 0) {
					continue;
				}
				using var competitors = state.MagnetStates.GetEnumerator();
				while (competitors.MoveNext()) {
					if (competitors.Current.Key == itemId
					    || competitors.Current.Value.CandidateBallId != magnet.CandidateBallId) {
						continue;
					}
					var other = competitors.Current.Value;
					if (other.CandidateDistanceSq < magnet.CandidateDistanceSq
					    || other.CandidateDistanceSq == magnet.CandidateDistanceSq
					    && competitors.Current.Key < itemId) {
						magnet.CandidateBallId = 0;
						break;
					}
				}
			}
		}

		private static void AcquireCandidates(ref PhysicsState state)
		{
			using var magnets = state.MagnetStates.GetEnumerator();
			while (magnets.MoveNext()) {
				var itemId = magnets.Current.Key;
				ref var magnet = ref magnets.Current.Value;
				var ballId = magnet.CandidateBallId;
				if (ballId == 0 || magnet.AttachedBallId != 0 || !state.Balls.ContainsKey(ballId)) {
					continue;
				}
				ref var ball = ref state.Balls.GetValueByRef(ballId);
				if (ball.AttachedMagnetId != 0) {
					continue;
				}
				var bitIndex = state.InsideOfs.GetOrCreateBitIndex(ballId);
				magnet.AttachedBallId = ballId;
				magnet.GrabbedBalls.SetBits(bitIndex, true);
				magnet.ReleasedBalls.SetBits(bitIndex, false);
				magnet.SaturationTicks = 0;
				ball.AttachedMagnetId = itemId;
				MagnetPhysics.UpdateMembership(itemId, ballId, true, ref state);
				state.EventQueue.Enqueue(new EventData(EventId.MagnetEventsBallGrabbed, itemId, ballId, true));
			}
		}

		private static void ApplyOwnedFields(ref PhysicsState state, float step)
		{
			if (step <= 0f) {
				return;
			}
			using var magnets = state.MagnetStates.GetEnumerator();
			while (magnets.MoveNext()) {
				var itemId = magnets.Current.Key;
				ref var magnet = ref magnets.Current.Value;
				if (!IsUsableOwner(in magnet, ref state) || !IsPrimaryForOwner(itemId, in magnet, ref state)
				    || !MagnetPhysics.HasActiveField(in magnet)) {
					continue;
				}
				ref var hinge = ref state.SpringHingeStates.GetValueByRef(magnet.HingeOwnerId);
				GetPose(in magnet, in hinge, out var pole, out _);
				using var balls = state.Balls.GetEnumerator();
				while (balls.MoveNext()) {
					ref var ball = ref balls.Current.Value;
					if (ball.IsFrozen || ball.Id == magnet.AttachedBallId) {
						continue;
					}
					var delta = ball.Position - pole;
					var distanceSq = math.lengthsq(delta);
					if (distanceSq <= MinimumValue || distanceSq >= magnet.Radius * magnet.Radius) {
						continue;
					}
					var distance = math.sqrt(distanceSq);
					var cutoff = CompactSupport(distanceSq, magnet.Radius * magnet.Radius);
					var accelerationMagnitude = MagnetPhysics.PhysicalForceMagnitude(distance, 0f,
						cutoff, in magnet);
					var acceleration = -delta / distance * accelerationMagnitude;
					var impulse = acceleration * ball.Mass * step;
					ball.Velocity += acceleration * step;
					ball.ExternalAcceleration += acceleration;
					var poleArm = pole - hinge.Static.Pivot;
					hinge.Movement.PendingMagneticAngularImpulse += math.dot(hinge.Static.Axis,
						math.cross(poleArm, -impulse));
				}
			}
		}

		private static void CommitOwnedHolds(ref PhysicsState state, float step)
		{
			using var magnets = state.MagnetStates.GetEnumerator();
			while (magnets.MoveNext()) {
				var itemId = magnets.Current.Key;
				ref var magnet = ref magnets.Current.Value;
				if (!IsUsableOwner(in magnet, ref state) || !IsPrimaryForOwner(itemId, in magnet, ref state)) {
					continue;
				}
				ref var hinge = ref state.SpringHingeStates.GetValueByRef(magnet.HingeOwnerId);
				if (magnet.AttachedBallId == 0 || !state.Balls.ContainsKey(magnet.AttachedBallId)) {
					SpringHingeVelocityPhysics.CommitFreeVelocity(ref hinge);
					continue;
				}
				ref var ball = ref state.Balls.GetValueByRef(magnet.AttachedBallId);
				GetPose(in magnet, in hinge, out _, out var target);
				if (!SolveHold(ref ball, ref hinge, in magnet, in target, step, out var saturated)) {
					ReleaseAttachment(itemId, ref magnet, ref state, true);
					SpringHingeVelocityPhysics.CommitFreeVelocity(ref hinge);
					continue;
				}

				var relativePosition = ball.Position - target;
				var materialArm = math.cross(hinge.Static.Axis, ball.Position - hinge.Static.Pivot);
				var relativeVelocity = ball.Velocity - materialArm * hinge.Movement.AngularVelocity;
				var separating = math.dot(relativePosition, relativeVelocity) > 0f;
				magnet.SaturationTicks = saturated && separating
					? (byte)math.min(byte.MaxValue, magnet.SaturationTicks + 1)
					: (byte)0;
				if (magnet.SaturationTicks >= SaturationReleaseTicks) {
					ReleaseAttachment(itemId, ref magnet, ref state, true);
				}
			}
		}

		internal static bool SolveHold(ref BallState ball, ref SpringHingeState hinge,
			in MagnetState magnet, in float3 target, float step, out bool saturated)
		{
			saturated = false;
			var mass = ball.Mass;
			var inertia = hinge.Static.Inertia;
			var stiffness = math.max(0f, magnet.HoldStiffness);
			var damping = math.max(0f, magnet.HoldDamping);
			var maxImpulse = math.max(0f, magnet.MaxHoldForce)
				* magnet.EffectiveCurrent * magnet.EffectiveCurrent * step;
			if (step <= 0f || mass <= 0f || inertia <= 0f || maxImpulse <= 0f
			    || stiffness <= 0f && damping <= 0f || !math.isfinite(step)
			    || !math.isfinite(mass) || !math.isfinite(inertia)
			    || !math.isfinite(stiffness) || !math.isfinite(damping)
			    || !math.isfinite(maxImpulse)) {
				return false;
			}

			var movement = hinge.Movement;
			var u = math.cross(hinge.Static.Axis, ball.Position - hinge.Static.Pivot);
			var error = ball.Position - target;
			var beta = step * step * stiffness + step * damping;
			var ballResponse = 1f + beta / mass;
			var constantImpulse = -step * stiffness * (error + step * ball.Velocity)
				- step * damping * ball.Velocity;
			var denominator = inertia + step * hinge.Static.Damping
				+ step * step * hinge.Static.Stiffness;
			if (denominator <= MinimumValue) {
				return false;
			}
			var baseAngularMomentum = inertia * movement.TickStartAngularVelocity
				+ step * movement.GravityTorque + movement.PendingMagneticAngularImpulse
				- step * hinge.Static.Stiffness * movement.TickStartAngleError;
			var coupledDenominator = denominator + beta * math.lengthsq(u) / ballResponse;
			var omega = (baseAngularMomentum - math.dot(u, constantImpulse) / ballResponse)
				/ coupledDenominator;
			var impulse = (constantImpulse + beta * u * omega) / ballResponse;
			if (!math.isfinite(omega) || !math.all(math.isfinite(impulse))) {
				return false;
			}
			var impulseLength = math.length(impulse);
			if (impulseLength > maxImpulse) {
				impulse *= maxImpulse / impulseLength;
				saturated = true;
				omega = (baseAngularMomentum - math.dot(u, impulse)) / denominator;
			}

			if (movement.ActiveStop != 0 && movement.ActiveStop * omega > 0f) {
				var freeImpulse = impulse;
				var freeOmega = omega;
				var freeSaturated = saturated;
				omega = 0f;
				impulse = constantImpulse / ballResponse;
				saturated = false;
				impulseLength = math.length(impulse);
				if (impulseLength > maxImpulse) {
					impulse *= maxImpulse / impulseLength;
					saturated = true;
				}
				var bearingImpulse = -inertia * movement.TickStartAngularVelocity
					- step * movement.GravityTorque - movement.PendingMagneticAngularImpulse
					+ step * hinge.Static.Stiffness * movement.TickStartAngleError
					+ math.dot(u, impulse);
				if (movement.ActiveStop * bearingImpulse > 0f) {
					impulse = freeImpulse;
					omega = freeOmega;
					saturated = freeSaturated;
				}
			}

			ball.Velocity += impulse / mass;
			ball.ExternalAcceleration += impulse / (mass * step);
			MagnetPhysics.DampHeldBallSpin(ref ball, step);
			hinge.Movement.AngularVelocity = omega;
			hinge.Movement.CommittedMagneticTorque =
				(hinge.Movement.PendingMagneticAngularImpulse - math.dot(u, impulse)) / step;
			hinge.Movement.VelocityCommitted = true;
			if (hinge.Movement.ActiveStop * omega < -MinimumValue) {
				hinge.Movement.ActiveStop = 0;
			}
			SpringHingeVelocityPhysics.RefreshContinuousAcceleration(ref hinge);
			return true;
		}

		internal static bool CanCapture(in BallState ball, in MagnetState magnet,
			in SpringHingeState hinge, in float3 pole, in float3 target)
			=> CanCaptureWithin(in ball, in magnet, in hinge, in pole, in target,
				magnet.GrabRadius);

		internal static float EstimateStationaryHingeCaptureSpeed(in MagnetState magnet,
			in float3 pole, in float3 target)
		{
			var availableWork = AvailableCaptureWork(in magnet, in pole, in target,
				magnet.GrabRadius);
			return availableWork > 0f && math.isfinite(availableWork)
				? math.sqrt(2f * availableWork)
				: 0f;
		}

		private static bool CanCaptureWithin(in BallState ball, in MagnetState magnet,
			in SpringHingeState hinge, in float3 pole, in float3 target, float workRadius)
		{
			if (ball.Mass <= MinimumValue || hinge.Static.Inertia <= MinimumValue
			    || magnet.Radius <= MinimumValue || workRadius <= 0f
			    || !math.isfinite(ball.Mass) || !math.isfinite(hinge.Static.Inertia)
			    || !math.all(math.isfinite(ball.Position)) || !math.all(math.isfinite(ball.Velocity))) {
				return false;
			}
			var availableWork = AvailableCaptureWork(in magnet, in pole, ball.Position,
				target, workRadius, ball.Mass);
			if (availableWork <= 0f) {
				return false;
			}

			var u = math.cross(hinge.Static.Axis, ball.Position - hinge.Static.Pivot);
			var relativeVelocity = ball.Velocity - u * hinge.Movement.TickStartAngularVelocity;
			var inverseInertia = 1f / hinge.Static.Inertia;
			var response = float3x3.identity / ball.Mass + Outer(u) * inverseInertia;
			var requiredImpulse = -math.mul(math.inverse(response), relativeVelocity);
			if (hinge.Movement.ActiveStop != 0
			    && hinge.Movement.ActiveStop * -math.dot(u, requiredImpulse) > 0f) {
				response = float3x3.identity / ball.Mass;
				requiredImpulse = -math.mul(math.inverse(response), relativeVelocity);
			}
			var requiredEnergy = -0.5f * math.dot(relativeVelocity, requiredImpulse);
			return math.isfinite(requiredEnergy) && requiredEnergy <= availableWork;
		}

		private static float AvailableCaptureWork(in MagnetState magnet, in float3 pole,
			in float3 target, float workRadius)
			=> AvailableCaptureWork(in magnet, in pole, in target, in target, workRadius, 1f);

		private static float AvailableCaptureWork(in MagnetState magnet, in float3 pole,
			in float3 ballPosition, in float3 target, float workRadius, float ballMass)
		{
			var delta = ballPosition - pole;
			var distanceSq = math.lengthsq(delta);
			if (distanceSq <= MinimumValue || distanceSq >= magnet.Radius * magnet.Radius) {
				return 0f;
			}
			var distance = math.sqrt(distanceSq);
			var cutoff = CompactSupport(distanceSq, magnet.Radius * magnet.Radius);
			var fieldForce = MagnetPhysics.PhysicalForceMagnitude(distance, 0f, cutoff, in magnet)
				* ballMass;
			var holdForce = math.max(0f, magnet.MaxHoldForce)
				* magnet.EffectiveCurrent * magnet.EffectiveCurrent;
			return math.min(fieldForce, holdForce)
			       * math.max(0f, workRadius - math.distance(ballPosition, target));
		}

		private static bool HasValidProxyGap(in BallState ball, in SpringHingeState hinge,
			in float3 target, ref PhysicsState state)
		{
			for (var colliderId = 0; colliderId < state.Colliders.Length; colliderId++) {
				if (state.Colliders.GetHeader(colliderId).Type != ColliderType.SpringHinge) {
					continue;
				}
				ref var collider = ref state.Colliders.SpringHinge(colliderId);
				if (collider.HingeOwnerId != hinge.Static.OwnerId) {
					continue;
				}
				var targetGap = collider.Distance(in hinge, in target, ball.Radius).Separation;
				var currentGap = collider.Distance(in hinge, ball.Position, ball.Radius).Separation;
				if (math.abs(targetGap) <= PhysicsConstants.PhysTouch
				    && currentGap >= -PhysicsConstants.PhysTouch) {
					return true;
				}
			}
			return false;
		}

		private static bool IsLegacyGrabbed(int ballId, ref PhysicsState state)
		{
			if (!state.InsideOfs.TryGetBitIndex(ballId, out var bitIndex)) {
				return false;
			}
			using var magnets = state.MagnetStates.GetEnumerator();
			while (magnets.MoveNext()) {
				if (!magnets.Current.Value.CoupleToHinge
				    && magnets.Current.Value.GrabbedBalls.IsSet(bitIndex)) {
					return true;
				}
			}
			return false;
		}

		private static bool IsUsableOwner(in MagnetState magnet, ref PhysicsState state)
			=> magnet.CoupleToHinge && magnet.HingeOwnerId != 0
			   && state.SpringHingeStates.ContainsKey(magnet.HingeOwnerId)
			   && magnet.MagnetType == MagnetType.Spatial
			   && magnet.Profile == MagnetForceProfile.Physical;

		private static bool IsPrimaryForOwner(int itemId, in MagnetState magnet, ref PhysicsState state)
		{
			using var magnets = state.MagnetStates.GetEnumerator();
			while (magnets.MoveNext()) {
				if (magnets.Current.Key < itemId && magnets.Current.Value.CoupleToHinge
				    && magnets.Current.Value.HingeOwnerId == magnet.HingeOwnerId) {
					return false;
				}
			}
			return true;
		}

		private static void ReleaseAttachment(int itemId, ref MagnetState magnet,
			ref PhysicsState state, bool suppressRegrab)
		{
			var ballId = magnet.AttachedBallId;
			if (ballId == 0) {
				return;
			}
			if (state.Balls.ContainsKey(ballId)) {
				ref var ball = ref state.Balls.GetValueByRef(ballId);
				if (ball.AttachedMagnetId == itemId) {
					ball.AttachedMagnetId = 0;
				}
			}
			if (state.InsideOfs.TryGetBitIndex(ballId, out var bitIndex)) {
				MagnetPhysics.ReleaseGrabbedBall(itemId, ref magnet, bitIndex, ballId,
					ref state, suppressRegrab);
			} else {
				magnet.AttachedBallId = 0;
			}
			magnet.AttachedBallId = 0;
			magnet.SaturationTicks = 0;
		}

		private static void GetPose(in MagnetState magnet, in SpringHingeState hinge,
			out float3 pole, out float3 target)
		{
			var angle = hinge.Movement.Angle;
			pole = hinge.Static.Pivot + SpringHingeVelocityPhysics.RotateAroundAxis(
				magnet.LocalPoleArm, hinge.Static.Axis, angle);
			target = hinge.Static.Pivot + SpringHingeVelocityPhysics.RotateAroundAxis(
				magnet.LocalHeldCentreArm, hinge.Static.Axis, angle);
		}

		private static float CompactSupport(float distanceSq, float radiusSq)
		{
			if (radiusSq <= MinimumValue || distanceSq >= radiusSq) {
				return 0f;
			}
			var remaining = 1f - distanceSq / radiusSq;
			return remaining * remaining;
		}

		private static float3x3 Outer(in float3 value)
			=> new(value * value.x, value * value.y, value * value.z);
	}
}
