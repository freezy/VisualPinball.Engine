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

using Unity.Burst;
using Unity.Mathematics;
using VisualPinball.Engine.Game;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	[BurstCompile(FloatPrecision.Medium, FloatMode.Fast, CompileSynchronously = true)]
	internal static class MagnetPhysics
	{
		internal const float VpxMagnetUpdateMs = 10f;
		private const float MinDistance = 0.0001f;
		private const float MinDistanceSq = MinDistance * MinDistance;
		private const float PhysicalMinimumPoleRadius = 1f;
		private const float AnnularPoleKernelNormalization = 3.864f;
		// Keep cylindrical Strength on the familiar VPX magnet authoring scale.
		// At full current, this converts the authored value to contact acceleration.
		private const float VpxStrengthScale = 1.5f / 56f;
		private const float PhysicalVelocityDamping = 0.02f;
		private const float MinEffectiveCurrent = 0.0001f;
		internal const float CylindricalContactTolerance = 1f;
		private const float CylindricalReleaseTolerance = 2f;

		[BurstCompile]
		internal static void Update(int itemId, ref MagnetState magnet, ref PhysicsState state, float physicsDiffTime)
		{
			AdvanceCoil(ref magnet, physicsDiffTime);
			if (!HasActiveField(in magnet)) {
				ReleaseGrabbedBalls(itemId, ref magnet, ref state, false);
				if (!state.InsideOfs.IsEmpty(itemId)) {
					ReleaseMembership(itemId, ref state);
				}
				return;
			}

			// keep the field pose fresh for enabled magnets before forces are applied
			var magnetVelocity = RefreshKinematicState(itemId, ref magnet, ref state);

			// only the VPX-compatible playfield path damps planar velocity; skip the
			// transcendental for spatial and physical magnets that never read it
			var vpxDamping = magnet.MagnetType == MagnetType.Playfield && magnet.Profile == MagnetForceProfile.VpxCompatible
				? math.pow(magnet.PlanarDamping, physicsDiffTime)
				: 0f;

			using (var enumerator = state.Balls.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var ball = ref enumerator.Current.Value;
					// a ball frozen by something else (e.g. a kicker capture) is off-limits;
					// spatial magnets hold with a force, not a freeze, so they never freeze a ball
					if (ball.IsFrozen) {
						ReleaseGrabbedBall(itemId, ref magnet, ref state, ball.Id);
						UpdateMembership(itemId, ball.Id, false, ref state);
						continue;
					}

					var affectsBall = IsBallInRange(in ball, in magnet) || IsGrabbedBall(in magnet, ref state, ball.Id);
					if (!affectsBall) {
						ReleaseGrabbedBall(itemId, ref magnet, ref state, ball.Id);
						ClearReleasedBall(ref magnet, ref state, ball.Id);
						UpdateMembership(itemId, ball.Id, false, ref state);
						continue;
					}

					UpdateMembership(itemId, ball.Id, true, ref state);
					if (UpdateGrab(itemId, ref magnet, ref state, ref ball, physicsDiffTime, magnetVelocity)) {
						continue;
					}

					switch (magnet.MagnetType) {
						case MagnetType.Spatial:
							ApplySpatialPhysicalForce(ref ball, in magnet, physicsDiffTime, magnetVelocity);
							break;
						case MagnetType.Cylindrical:
							ApplyCylindricalPhysicalForce(ref ball, in magnet, physicsDiffTime, magnetVelocity);
							break;
						default:
							switch (magnet.Profile) {
								case MagnetForceProfile.VpxCompatible:
									ApplyVpxCompatibleForce(ref ball, in magnet, physicsDiffTime, vpxDamping);
									break;
								case MagnetForceProfile.Physical:
									ApplyPhysicalForce(ref ball, in magnet, physicsDiffTime, magnetVelocity.xy);
									break;
							}
							break;
					}
				}
			}
		}

		/// <summary>
		/// Advances the normalized coil current toward its command. Physical force is
		/// proportional to current squared; VPX Compatible remains instantaneous.
		/// Time is expressed in the engine's normalized 10 ms units.
		/// </summary>
		internal static void AdvanceCoil(ref MagnetState magnet, float physicsDiffTime)
		{
			var command = magnet.IsEnabled ? math.saturate(magnet.CommandedPower) : 0f;
			if (!UsesPhysicalResponse(in magnet)) {
				magnet.EffectiveCurrent = command;
				magnet.EffectiveStrength = magnet.Strength * command;
				return;
			}

			var timeConstant = command > magnet.EffectiveCurrent ? magnet.RiseTime : magnet.FallTime;
			if (timeConstant <= MinDistance || physicsDiffTime <= 0f) {
				magnet.EffectiveCurrent = command;
			} else {
				// Implicit Euler is stable for long frames and approximates the RL
				// exponential without a transcendental in the 1 kHz loop.
				var alpha = math.saturate(physicsDiffTime / (timeConstant + physicsDiffTime));
				magnet.EffectiveCurrent = math.lerp(magnet.EffectiveCurrent, command, alpha);
				if (math.abs(magnet.EffectiveCurrent - command) <= MinEffectiveCurrent) {
					magnet.EffectiveCurrent = command;
				}
			}
			magnet.EffectiveStrength = magnet.Strength * magnet.EffectiveCurrent * magnet.EffectiveCurrent;
		}

		internal static float3 RefreshKinematicState(int itemId, ref MagnetState magnet, ref PhysicsState state)
		{
			if (!magnet.IsKinematic || !state.KinematicTransforms.TryGetValue(itemId, out var matrix)) {
				return float3.zero;
			}

			ApplyKinematicTransform(ref magnet, in matrix);
			return GetKinematicVelocity(itemId, in magnet, ref state);
		}

		internal static void ApplyKinematicTransform(ref MagnetState magnet, in float4x4 matrix)
		{
			magnet.Position = matrix.c3.xy;
			magnet.Height = matrix.c3.z;
		}

		internal static void ReleaseGrabbedBalls(int itemId, ref MagnetState magnet, ref PhysicsState state, bool suppressRegrab)
		{
			// the ball is a live physics object throughout the hold, so releasing it is
			// just dropping the hold force — it keeps whatever velocity it currently has
			if (magnet.GrabbedBalls.Value != 0UL) {
				for (var bitIndex = 0; bitIndex < 64; bitIndex++) {
					if (!magnet.GrabbedBalls.IsSet(bitIndex)) {
						continue;
					}
					magnet.GrabbedBalls.SetBits(bitIndex, false);
					if (suppressRegrab) {
						magnet.ReleasedBalls.SetBits(bitIndex, true);
					}
					if (state.InsideOfs.TryGetBallIdAtBitIndex(bitIndex, out var ballId)) {
						state.EventQueue.Enqueue(new EventData(EventId.MagnetEventsBallReleased, itemId, ballId, true));
					}
				}
			}

			if (!suppressRegrab) {
				magnet.ReleasedBalls = default;
			}
		}

		internal static void EjectGrabbedBalls(int itemId, ref MagnetState magnet, ref PhysicsState state, float speed, float angleDeg, float verticalAngleDeg = 0f)
		{
			if (magnet.GrabbedBalls.Value == 0UL) {
				return;
			}

			// a ball thrown from a moving magnet keeps the carrier velocity, same as
			// releasing the coil mid-move does; refresh the pose first so a kinematic
			// magnet ejected between ticks uses its current hold point, not last tick's
			var carrierVelocity = RefreshKinematicState(itemId, ref magnet, ref state);

			for (var bitIndex = 0; bitIndex < 64; bitIndex++) {
				if (!magnet.GrabbedBalls.IsSet(bitIndex)) {
					continue;
				}
				if (state.InsideOfs.TryGetBallIdAtBitIndex(bitIndex, out var ballId)) {
					if (state.Balls.ContainsKey(ballId)) {
						ref var ball = ref state.Balls.GetValueByRef(ballId);
						if (magnet.MagnetType != MagnetType.Playfield) {
							ApplySpatialEject(ref ball, speed, angleDeg, verticalAngleDeg, carrierVelocity);
						} else {
							ApplyPlanarEject(ref ball, speed, angleDeg, carrierVelocity.xy);
						}
					}
					state.EventQueue.Enqueue(new EventData(EventId.MagnetEventsBallReleased, itemId, ballId, true));
				}
				magnet.GrabbedBalls.SetBits(bitIndex, false);
				magnet.ReleasedBalls.SetBits(bitIndex, true);
			}
		}

		internal static void ApplyVpxCompatibleForce(ref BallState ball, in MagnetState magnet, float physicsDiffTime)
			=> ApplyVpxCompatibleForce(ref ball, in magnet, physicsDiffTime, math.pow(magnet.PlanarDamping, physicsDiffTime));

		internal static void ApplyVpxCompatibleForce(ref BallState ball, in MagnetState magnet, float physicsDiffTime, float damping)
		{
			var delta = ball.Position.xy - magnet.Position;
			var distance = math.length(delta);
			if (distance <= MinDistance || magnet.Radius <= MinDistance) {
				return;
			}

			// cvpmMagnet.AttractBall: ratio = dist / (1.5*Size), then the damping wraps
			// both the old velocity and the impulse: (vel - dir*force) * 0.985
			var ratio = distance / (1.5f * magnet.Radius);
			var force = magnet.EffectiveStrength * math.exp(-0.2f / ratio) / (ratio * ratio * 56f) * 1.5f;
			var direction = delta / distance;

			var velocity = (ball.Velocity.xy - direction * force * physicsDiffTime) * damping;
			ball.Velocity = new float3(velocity.x, velocity.y, ball.Velocity.z);
		}

		internal static void ApplyPhysicalForce(ref BallState ball, in MagnetState magnet, float physicsDiffTime, float2 magnetVelocity = default)
		{
			var delta = ball.Position.xy - magnet.Position;
			var distanceSq = math.lengthsq(delta);
			if (distanceSq <= MinDistanceSq) {
				return;
			}

			var distance = math.sqrt(distanceSq);
			var height = math.max(0f, ball.Position.z - magnet.Height);
			var cutoff = CompactSupport(distanceSq, magnet.Radius * magnet.Radius);
			if (magnet.HeightRange > 0f) {
				cutoff *= CompactSupport(height * height, magnet.HeightRange * magnet.HeightRange);
			}
			var force = PhysicalForceMagnitude(distance, height, cutoff, in magnet);
			var direction = delta / distance;
			var velocity = ball.Velocity.xy - direction * force * physicsDiffTime;

			var damping = math.saturate(math.abs(force) * PhysicalVelocityDamping * physicsDiffTime);
			velocity = magnetVelocity + (velocity - magnetVelocity) * (1f - damping);

			ball.Velocity = new float3(velocity.x, velocity.y, ball.Velocity.z);
		}

		internal static void ApplySpatialPhysicalForce(ref BallState ball, in MagnetState magnet, float physicsDiffTime, float3 magnetVelocity = default)
		{
			var delta = ball.Position - Center3D(in magnet);
			var distanceSq = math.lengthsq(delta);
			if (distanceSq <= MinDistanceSq) {
				return;
			}

			var distance = math.sqrt(distanceSq);
			var cutoff = CompactSupport(distanceSq, magnet.Radius * magnet.Radius);
			var force = PhysicalForceMagnitude(distance, 0f, cutoff, in magnet);
			var direction = delta / distance;
			var velocity = ball.Velocity - direction * force * physicsDiffTime;

			var damping = math.saturate(math.abs(force) * PhysicalVelocityDamping * physicsDiffTime);
			velocity = magnetVelocity + (velocity - magnetVelocity) * (1f - damping);

			ball.Velocity = velocity;
		}

		internal static void ApplyCylindricalPhysicalForce(ref BallState ball, in MagnetState magnet, float physicsDiffTime, float3 magnetVelocity = default)
		{
			var surface = CylinderSurface(in ball, in magnet);
			if (surface.CenterDistance <= MinDistance) {
				return;
			}

			var force = CylindricalSurfaceForceMagnitude(surface.AirGap, in magnet);
			var direction = surface.Offset / surface.CenterDistance;
			var relativeVelocity = ball.Velocity - magnetVelocity;
			var normalVelocity = direction * math.dot(relativeVelocity, direction);
			var tangentialVelocity = relativeVelocity - normalVelocity;
			normalVelocity -= direction * force * physicsDiffTime;
			var damping = math.saturate(math.abs(force) * PhysicalVelocityDamping * math.max(0f, magnet.CylindricalDamping) * physicsDiffTime);
			ball.Velocity = magnetVelocity + tangentialVelocity + normalVelocity * (1f - damping);
		}

		internal static void ApplyVpxCompatibleGrab(ref BallState ball, in MagnetState magnet, float2 magnetVelocity = default)
		{
			ball.Position = new float3(magnet.Position.x, magnet.Position.y, ball.Position.z);
			ball.EventPosition = new float3(magnet.Position.x, magnet.Position.y, ball.EventPosition.z);
			ball.Velocity = new float3(magnetVelocity.x, magnetVelocity.y, ball.Velocity.z);
			ball.OldVelocity = new float3(magnetVelocity.x, magnetVelocity.y, ball.OldVelocity.z);
			ball.AngularMomentum = float3.zero;
		}

		/// <summary>
		/// 3-D critically-damped spring pulling the ball toward the moving hold point.
		/// The ball stays a live physics object — it renders and collides normally, and
		/// the capped impulse means a hard enough hit from another ball overcomes the
		/// hold and knocks it loose (the next tick it leaves the grab radius and is
		/// released). This is the spatial analog of <see cref="ApplyPhysicalHold"/>.
		/// </summary>
		internal static void ApplySpatialPhysicalHold(ref BallState ball, in MagnetState magnet, float physicsDiffTime, float3 magnetVelocity = default)
		{
			var offset = ball.Position - Center3D(in magnet);
			var relativeVelocity = ball.Velocity - magnetVelocity;
			var holdStrength = math.abs(magnet.EffectiveStrength);
			if (holdStrength <= MinDistance) {
				return;
			}
			var holdRadius = math.max(magnet.GrabRadius, math.max(magnet.PoleRadius, PhysicalMinimumPoleRadius));
			var stiffness = holdStrength / holdRadius;
			var damping = 2f * math.sqrt(stiffness);
			var impulse = (-offset * stiffness - relativeVelocity * damping) * physicsDiffTime;
			var maxImpulse = holdStrength * physicsDiffTime;
			var impulseLenSq = math.lengthsq(impulse);
			var maxImpulseSq = maxImpulse * maxImpulse;

			if (impulseLenSq > maxImpulseSq && impulseLenSq > MinDistanceSq) {
				impulse *= maxImpulse / math.sqrt(impulseLenSq);
			}

			ball.Velocity += impulse;
			ball.AngularMomentum *= 1f - math.saturate(physicsDiffTime * 0.5f);
		}

		internal static void ApplyCylindricalPhysicalHold(ref BallState ball, in MagnetState magnet, float physicsDiffTime, float3 magnetVelocity = default)
		{
			// Unlike a point magnet, the stable hold location is the collider surface.
			// Keep pressing inward so collision friction can slow a glancing ball without
			// teleporting or freezing it.
			ApplyCylindricalPhysicalForce(ref ball, in magnet, physicsDiffTime, magnetVelocity);
			ball.AngularMomentum *= 1f - math.saturate(physicsDiffTime * 0.5f * math.max(0f, magnet.CylindricalDamping));
		}

		internal static void ApplyPhysicalHold(ref BallState ball, in MagnetState magnet, float physicsDiffTime, float2 magnetVelocity = default)
		{
			var offset = ball.Position.xy - magnet.Position;
			var velocity = ball.Velocity.xy;
			var relativeVelocity = velocity - magnetVelocity;
			var holdStrength = math.abs(magnet.EffectiveStrength);
			if (holdStrength <= MinDistance) {
				return;
			}
			var holdRadius = math.max(magnet.GrabRadius, math.max(magnet.PoleRadius, PhysicalMinimumPoleRadius));
			var stiffness = holdStrength / holdRadius;
			var damping = 2f * math.sqrt(stiffness);
			var impulse = (-offset * stiffness - relativeVelocity * damping) * physicsDiffTime;
			var maxImpulse = holdStrength * physicsDiffTime;
			var impulseLenSq = math.lengthsq(impulse);
			var maxImpulseSq = maxImpulse * maxImpulse;

			if (impulseLenSq > maxImpulseSq && impulseLenSq > MinDistanceSq) {
				impulse *= maxImpulse / math.sqrt(impulseLenSq);
			}

			velocity += impulse;
			ball.Velocity = new float3(velocity.x, velocity.y, ball.Velocity.z);
			ball.AngularMomentum *= 1f - math.saturate(physicsDiffTime * 0.5f);
		}

		internal static void ApplyPlanarEject(ref BallState ball, float speed, float angleDeg, float2 carrierVelocity = default)
		{
			var angleRad = math.radians(angleDeg);
			var velocity = carrierVelocity + new float2(
				math.sin(angleRad) * speed,
				-math.cos(angleRad) * speed
			);
			ball.Velocity = new float3(velocity.x, velocity.y, ball.Velocity.z);
			ball.OldVelocity = new float3(velocity.x, velocity.y, ball.OldVelocity.z);
			ball.AngularMomentum = float3.zero;
		}

		internal static void ApplySpatialEject(ref BallState ball, float speed, float angleDeg, float verticalAngleDeg, float3 carrierVelocity = default)
		{
			var angleRad = math.radians(angleDeg);
			var verticalRad = math.radians(verticalAngleDeg);
			var horizontalSpeed = speed * math.cos(verticalRad);
			var velocity = carrierVelocity + new float3(
				math.sin(angleRad) * horizontalSpeed,
				-math.cos(angleRad) * horizontalSpeed,
				math.sin(verticalRad) * speed
			);

			ball.Velocity = velocity;
			ball.OldVelocity = velocity;
			ball.AngularMomentum = float3.zero;
		}

		internal static float3 Center3D(in MagnetState magnet)
		{
			return new float3(magnet.Position.x, magnet.Position.y, magnet.Height);
		}

		internal static bool IsBallInRange(in BallState ball, in MagnetState magnet)
		{
			if (magnet.Radius <= 0f) {
				return false;
			}
			switch (magnet.MagnetType) {
				case MagnetType.Spatial:
					return math.lengthsq(ball.Position - Center3D(in magnet)) <= magnet.Radius * magnet.Radius;
				case MagnetType.Cylindrical:
					var surface = CylinderSurface(in ball, in magnet);
					return surface.AirGap <= magnet.Radius;
			}
			if (magnet.HeightRange > 0f && (ball.Position.z < magnet.Height || ball.Position.z > magnet.Height + magnet.HeightRange)) {
				return false;
			}
			return math.lengthsq(ball.Position.xy - magnet.Position) <= magnet.Radius * magnet.Radius;
		}

		/// <summary>
		/// A held ball stays owned by the magnet even when a moving field carries its
		/// center outside the coarse influence volume. The profile-specific hold logic
		/// is responsible for deciding whether the ball has actually broken free.
		/// </summary>
		private static bool IsGrabbedBall(in MagnetState magnet, ref PhysicsState state, int ballId)
			=> magnet.GrabbedBalls.Value != 0UL
			   && state.InsideOfs.TryGetBitIndex(ballId, out var bitIndex)
			   && magnet.GrabbedBalls.IsSet(bitIndex);

		/// <summary>
		/// Shared grab bookkeeping for all magnet kinds. Point magnets use their
		/// authored grab radius. Cylindrical magnets acquire automatically at contact
		/// and use a small internal release hysteresis, so there is no second surface
		/// distance for creators to tune.
		/// </summary>
		private static bool UpdateGrab(int itemId, ref MagnetState magnet, ref PhysicsState state, ref BallState ball, float physicsDiffTime, float3 magnetVelocity)
		{
			// plain attraction magnets never grab; skip the bookkeeping entirely
			if (magnet.GrabRadius <= 0f && magnet.GrabbedBalls.Value == 0UL && magnet.ReleasedBalls.Value == 0UL) {
				return false;
			}
			if (magnet.MagnetType == MagnetType.Cylindrical) {
				return UpdateCylindricalGrab(itemId, ref magnet, ref state, ref ball, physicsDiffTime, magnetVelocity);
			}

			var bitIndex = state.InsideOfs.GetOrCreateBitIndex(ball.Id);
			var isGrabbed = magnet.GrabbedBalls.IsSet(bitIndex);
			var usesPhysicalCapture = UsesPhysicalResponse(in magnet);
			var distanceSq = GrabDistanceSq(in ball, in magnet);
			var hasGrabForce = usesPhysicalCapture
				? math.abs(magnet.EffectiveStrength) > MinDistance
				: magnet.EffectiveStrength > 0f;
			var isInGrabRange = magnet.GrabRadius > 0f &&
				                    hasGrabForce &&
				                    distanceSq <= magnet.GrabRadius * magnet.GrabRadius;

			if (!isInGrabRange) {
				magnet.ReleasedBalls.SetBits(bitIndex, false);
				if (isGrabbed) {
					ReleaseGrabbedBall(itemId, ref magnet, bitIndex, ball.Id, ref state, false);
				}
				return false;
			}

			if (magnet.ReleasedBalls.IsSet(bitIndex)) {
				return false;
			}

			if (!isGrabbed) {
				if (usesPhysicalCapture && !CanCapturePhysical(in ball, in magnet, distanceSq, magnetVelocity)) {
					return false;
				}
				magnet.GrabbedBalls.SetBits(bitIndex, true);
				state.EventQueue.Enqueue(new EventData(EventId.MagnetEventsBallGrabbed, itemId, ball.Id, true));
			}
			switch (magnet.MagnetType) {
				case MagnetType.Spatial:
					ApplySpatialPhysicalHold(ref ball, in magnet, physicsDiffTime, magnetVelocity);
					break;
				default:
					switch (magnet.Profile) {
						case MagnetForceProfile.Physical:
							ApplyPhysicalHold(ref ball, in magnet, physicsDiffTime, magnetVelocity.xy);
							break;
						default:
							ApplyVpxCompatibleGrab(ref ball, in magnet, magnetVelocity.xy);
							break;
					}
					break;
			}
			return true;
		}

		private static bool UpdateCylindricalGrab(int itemId, ref MagnetState magnet, ref PhysicsState state, ref BallState ball, float physicsDiffTime, float3 magnetVelocity)
		{
			var bitIndex = state.InsideOfs.GetOrCreateBitIndex(ball.Id);
			var isGrabbed = magnet.GrabbedBalls.IsSet(bitIndex);
			var surface = CylinderSurface(in ball, in magnet);
			var grabDistance = isGrabbed ? CylindricalReleaseTolerance : CylindricalContactTolerance;
			var isInGrabRange = magnet.GrabRadius > 0f &&
			                    math.abs(magnet.EffectiveStrength) > MinDistance &&
			                    surface.AirGap <= grabDistance;

			if (!isInGrabRange) {
				magnet.ReleasedBalls.SetBits(bitIndex, false);
				if (isGrabbed) {
					ReleaseGrabbedBall(itemId, ref magnet, bitIndex, ball.Id, ref state, false);
				}
				return false;
			}
			if (magnet.ReleasedBalls.IsSet(bitIndex)) {
				return false;
			}
			if (!isGrabbed) {
				if (!CanCaptureCylindrical(in ball, in magnet, in surface, magnetVelocity)) {
					return false;
				}
				magnet.GrabbedBalls.SetBits(bitIndex, true);
				state.EventQueue.Enqueue(new EventData(EventId.MagnetEventsBallGrabbed, itemId, ball.Id, true));
			}

			ApplyCylindricalPhysicalHold(ref ball, in magnet, physicsDiffTime, magnetVelocity);
			return true;
		}

		/// <summary>
		/// A physical grab starts only when the available hold acceleration can arrest
		/// relative motion before the ball crosses the remaining grab volume.
		/// </summary>
		internal static bool CanCapturePhysical(in BallState ball, in MagnetState magnet, float distanceSq, float3 magnetVelocity)
		{
			var distance = math.sqrt(math.max(0f, distanceSq));
			var remainingDistance = math.max(0f, magnet.GrabRadius - distance);
			var relativeSpeedSq = magnet.MagnetType != MagnetType.Playfield
				? math.lengthsq(ball.Velocity - magnetVelocity)
				: math.lengthsq(ball.Velocity.xy - magnetVelocity.xy);
			var availableAcceleration = math.abs(magnet.EffectiveStrength);
			return relativeSpeedSq <= 2f * availableAcceleration * remainingDistance;
		}

		/// <summary>
		/// A cylindrical contact ignores tangential speed because motion along the
		/// surface does not break the hold. Separating normal speed is compared with
		/// the work available in the remaining magnetic field, so stronger magnets
		/// can catch a faster rebound without an authored grab distance.
		/// </summary>
		internal static bool CanCaptureCylindrical(in BallState ball, in MagnetState magnet, in CylinderSurfaceData surface, float3 magnetVelocity)
		{
			if (surface.CenterDistance <= MinDistance) {
				return false;
			}
			var direction = surface.Offset / surface.CenterDistance;
			var separatingSpeed = math.max(0f, math.dot(ball.Velocity - magnetVelocity, direction));
			var availableWork = CylindricalAvailableWork(surface.AirGap, in magnet);
			return separatingSpeed * separatingSpeed <= 2f * availableWork;
		}

		/// <summary>
		/// Compact approximation to the lateral gradient of B squared above a finite,
		/// axisymmetric pole face. The radial force is zero on-axis, strongest in an
		/// annulus around the pole, and falls with the fifth power in the far field.
		/// </summary>
		internal static float PhysicalForceMagnitude(float radialDistance, float axialDistance, float cutoff, in MagnetState magnet)
		{
			if (cutoff <= 0f) {
				return 0f;
			}
			var poleRadius = math.max(PhysicalMinimumPoleRadius, magnet.PoleRadius);
			var radialRatio = radialDistance / poleRadius;
			var axialRatio = axialDistance / poleRadius;
			var denominator = 1f + radialRatio * radialRatio + axialRatio * axialRatio;
			var kernel = AnnularPoleKernelNormalization * radialRatio / (denominator * denominator * denominator);
			return math.abs(magnet.EffectiveStrength) * kernel * cutoff / (poleRadius * poleRadius);
		}

		/// <summary>
		/// Surface-normal attraction for an exposed cylindrical pole. The air gap is
		/// measured from the ball skin to the finite cylinder, making side and cap
		/// contact equivalent. Strength controls the contact force on the familiar
		/// VPX authoring scale, while Influence Distance defines a smooth decay to
		/// zero, with half force at half distance.
		/// </summary>
		internal static float CylindricalSurfaceForceMagnitude(float airGap, in MagnetState magnet)
		{
			return math.abs(magnet.EffectiveStrength) * VpxStrengthScale * CylindricalFalloff(airGap, magnet.Radius);
		}

		private static float CylindricalFalloff(float airGap, float influenceDistance)
		{
			if (influenceDistance <= MinDistance || airGap >= influenceDistance) {
				return 0f;
			}
			var ratio = math.saturate(airGap / influenceDistance);
			return 1f - ratio * ratio * (3f - 2f * ratio);
		}

		private static float CylindricalAvailableWork(float airGap, in MagnetState magnet)
		{
			if (magnet.Radius <= MinDistance || airGap >= magnet.Radius) {
				return 0f;
			}
			var ratio = math.saturate(airGap / magnet.Radius);
			var ratioSq = ratio * ratio;
			var normalizedWork = 0.5f - ratio + ratioSq * ratio - 0.5f * ratioSq * ratioSq;
			return math.abs(magnet.EffectiveStrength) * VpxStrengthScale * magnet.Radius * math.max(0f, normalizedWork);
		}

		internal static CylinderSurfaceData CylinderSurface(in BallState ball, in MagnetState magnet)
		{
			var radialOffset = ball.Position.xy - magnet.Position;
			var radialDistance = math.length(radialOffset);
			var cylinderRadius = math.max(0f, magnet.CylinderRadius);
			var radialDirection = radialDistance > MinDistance ? radialOffset / radialDistance : new float2(1f, 0f);
			var sidePoint = new float3(
				magnet.Position + radialDirection * cylinderRadius,
				magnet.CylinderHeight > 0f
					? math.clamp(ball.Position.z, magnet.Height, magnet.Height + magnet.CylinderHeight)
					: ball.Position.z
			);
			var closestPoint = sidePoint;

			if (magnet.CylinderHeight > 0f) {
				var capPlanar = radialDistance > cylinderRadius
					? magnet.Position + radialDirection * cylinderRadius
					: ball.Position.xy;
				var bottomPoint = new float3(capPlanar, magnet.Height);
				var topPoint = new float3(capPlanar, magnet.Height + magnet.CylinderHeight);
				var sideDistanceSq = math.lengthsq(ball.Position - sidePoint);
				var bottomDistanceSq = math.lengthsq(ball.Position - bottomPoint);
				var topDistanceSq = math.lengthsq(ball.Position - topPoint);
				if (bottomDistanceSq < sideDistanceSq && bottomDistanceSq <= topDistanceSq) {
					closestPoint = bottomPoint;
				} else if (topDistanceSq < sideDistanceSq) {
					closestPoint = topPoint;
				}
			}

			var offset = ball.Position - closestPoint;
			var centerDistance = math.length(offset);
			return new CylinderSurfaceData {
				Offset = offset,
				CenterDistance = centerDistance,
				AirGap = math.max(0f, centerDistance - ball.Radius)
			};
		}

		private static float GrabDistanceSq(in BallState ball, in MagnetState magnet)
		{
			return magnet.MagnetType == MagnetType.Spatial
				? math.lengthsq(ball.Position - Center3D(in magnet))
				: math.lengthsq(ball.Position.xy - magnet.Position);
		}

		private static float CompactSupport(float distanceSq, float radiusSq)
		{
			if (radiusSq <= MinDistanceSq || distanceSq >= radiusSq) {
				return 0f;
			}
			var remaining = 1f - distanceSq / radiusSq;
			return remaining * remaining;
		}

		private static bool UsesPhysicalResponse(in MagnetState magnet)
			=> magnet.MagnetType != MagnetType.Playfield || magnet.Profile == MagnetForceProfile.Physical;

		private static bool HasActiveField(in MagnetState magnet)
			=> UsesPhysicalResponse(in magnet)
				? magnet.EffectiveCurrent > MinEffectiveCurrent && math.abs(magnet.Strength) > MinDistance
				: magnet.IsEnabled && magnet.CommandedPower > 0f;

		private static float3 GetKinematicVelocity(int itemId, in MagnetState magnet, ref PhysicsState state)
		{
			return state.GetKinematicVelocityAt(itemId, Center3D(in magnet));
		}

		private static void ReleaseGrabbedBall(int itemId, ref MagnetState magnet, ref PhysicsState state, int ballId)
		{
			if (!state.InsideOfs.TryGetBitIndex(ballId, out var bitIndex)) {
				return;
			}
			ReleaseGrabbedBall(itemId, ref magnet, bitIndex, ballId, ref state, false);
		}

		private static void ReleaseGrabbedBall(int itemId, ref MagnetState magnet, int bitIndex, int ballId, ref PhysicsState state, bool suppressRegrab)
		{
			if (!magnet.GrabbedBalls.IsSet(bitIndex)) {
				return;
			}
			magnet.GrabbedBalls.SetBits(bitIndex, false);
			if (suppressRegrab) {
				magnet.ReleasedBalls.SetBits(bitIndex, true);
			}
			state.EventQueue.Enqueue(new EventData(EventId.MagnetEventsBallReleased, itemId, ballId, true));
		}

		private static void ClearReleasedBall(ref MagnetState magnet, ref PhysicsState state, int ballId)
		{
			if (state.InsideOfs.TryGetBitIndex(ballId, out var bitIndex)) {
				magnet.ReleasedBalls.SetBits(bitIndex, false);
			}
		}

		private static void ReleaseMembership(int itemId, ref PhysicsState state)
		{
			var ballIds = state.InsideOfs.GetIdsOfBallsInsideItem(itemId);
			for (var i = 0; i < ballIds.Length; i++) {
				var ballId = ballIds[i];
				state.InsideOfs.SetOutsideOf(itemId, ballId);
				state.EventQueue.Enqueue(new EventData(EventId.MagnetEventsBallExited, itemId, ballId, true));
			}
		}

		private static void UpdateMembership(int itemId, int ballId, bool isInside, ref PhysicsState state)
		{
			var wasInside = state.InsideOfs.IsInsideOf(itemId, ballId);
			if (isInside == wasInside) {
				return;
			}

			if (isInside) {
				state.InsideOfs.SetInsideOf(itemId, ballId);
				state.EventQueue.Enqueue(new EventData(EventId.MagnetEventsBallEntered, itemId, ballId, true));
			} else {
				state.InsideOfs.SetOutsideOf(itemId, ballId);
				state.EventQueue.Enqueue(new EventData(EventId.MagnetEventsBallExited, itemId, ballId, true));
			}
		}

		internal struct CylinderSurfaceData
		{
			internal float3 Offset;
			internal float CenterDistance;
			internal float AirGap;
		}
	}
}
