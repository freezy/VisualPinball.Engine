// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	internal static class PlungerVelocityPhysics
	{
		/// <summary>
		/// Speed below which (in m/s, towards the ball) a mechanical plunger
		/// motion on an auto plunger counts as a release.
		/// </summary>
		private const float AutoPlungerReleaseSpeed = -1f;

		/// <summary>
		/// Length of the synthetic Launch Ball press, in physics steps (ms).
		/// </summary>
		private const int AutoFireDuration = 101;

		internal static void UpdateVelocities(ref PlungerMovementState movement, ref PlungerVelocityState velocity,
			in PlungerStaticState staticState, in PlungerInputState input)
		{
			// figure our current position in relative coordinates (0.0-1.0,
			// where 0.0 is the maximum forward position and 1.0 is the
			// maximum retracted position)
			var pos = (movement.Position - staticState.FrameEnd) / staticState.FrameLen;

			var isMech = staticState.IsMechPlunger;
			var autoPlunger = staticState.IsAutoPlunger;

			// The collider reads these during the collision phase of this step.
			movement.HasSensor = isMech && input.HasSensor;
			movement.SensorHitVelocity = movement.HasSensor ? input.GetHitVelocity(staticState.RestPosition) : 0f;

			// check which forces are acting on us
			if (movement.FireTimer > 0) {
				// Fire mode.  In this mode, we're moving freely under the spring
				// forces at the speed we calculated when we initiated the release.
				// Simply leave the speed unchanged.
				//
				// Decrement the release mode timer.  The mode ends after the
				// timeout elapses, even if the mech plunger hasn't actually
				// come to rest.  This ensures that we don't get stuck in this
				// mode, and also allows us to sync up again with the real
				// plunger after a respectable pause if the user is just
				// moving it around a lot.
				movement.Speed = movement.FireSpeed;
				--movement.FireTimer;

			} else if (velocity.AutoFireTimer > 0) {
				// The Auto Fire timer is running.  We start this timer when the
				// player pulls back and releases the mechanical plunger while
				// we're operating as an auto plunger, which holds a synthetic
				// Launch Ball press.  The input side releases that press once
				// the timer reaches zero.
				--velocity.AutoFireTimer;

			} else if (isMech && autoPlunger && input.RawVelocity * PlungerSensorState.KalmanUnitScale < AutoPlungerReleaseSpeed) {
				// Release motion detected in Auto Plunger mode.
				//
				// An Auto Plunger simulates a solenoid-driven ball launcher
				// on a table like Medieval Madness.  On this type of game,
				// the original machine doesn't have a spring-loaded plunger
				// for the user to operate manually.  The user-operated control
				// is instead a button of some kind.  But on virtual cabinets,
				// the mechanical plunger doesn't just magically disappear when
				// you load such a table, so we use the mech plunger to simulate
				// the button: you pull it back and let it go, and rather than
				// simulating the motion directly on the software plunger, we
				// turn it into a synthetic Launch Ball press of 100 ms.  The
				// table then acts exactly like it would if the player had
				// pressed the Launch Ball button.
				velocity.AutoFireTimer = AutoFireDuration;
				velocity.AutoFireCount++;

			} else if (velocity.PullForce != 0.0f) {
				// A "pull" force is in effect.  This is a *simulated* pull, so
				// it overrides the real physical plunger position.
				//
				// Simply update the model speed by applying the acceleration
				// due to the pull force.
				//
				// Force = mass*acceleration -> a = F/m.  Increase the speed
				// by the acceleration.  Technically we're calculating dv = a dt,
				// but we can elide the elapsed time factor because it's
				// effectively a constant that's implicitly folded into the
				// pull force value.
				movement.Speed += velocity.PullForce / Plunger.PlungerMass;

				if (!velocity.AddRetractMotion) {
					// this is the normal PullBack branch

					// if we're already at the maximum retracted position, stop
					if (movement.Position > staticState.FrameStart) {
						movement.Speed = 0.0f;
						movement.Position = staticState.FrameStart;
					}

					// if we're already at the minimum retracted position, stop
					if (movement.Position < staticState.FrameEnd + staticState.RestPosition * staticState.FrameLen) {
						movement.Speed = 0.0f;
						movement.Position = staticState.FrameEnd + staticState.RestPosition * staticState.FrameLen;
					}

				} else {
					// this is the PullBackandRetract branch

					// after reaching the max. position the plunger should retract until it reaches the min. position and then start again
					// if we're already at the maximum retracted position, reverse
					if (movement.Position >= staticState.FrameStart && velocity.PullForce > 0) {
						movement.Speed = 0.0f;
						movement.Position = staticState.FrameStart;
						velocity.RetractWaitLoop++;
						if (velocity.RetractWaitLoop > 1000) { // 1 sec, related to PHYSICS_STEPTIME
							velocity.PullForce = -velocity.InitialSpeed;
							movement.Position = staticState.FrameStart;
							movement.RetractMotion = true;
							velocity.RetractWaitLoop = 0;
						}
					}

					// if we're already at the minimum retracted position, start again
					if (movement.Position <= staticState.FrameEnd + staticState.RestPosition * staticState.FrameLen && velocity.PullForce <= 0) {
						movement.Speed = 0.0f;
						velocity.PullForce = velocity.InitialSpeed;
						movement.Position = staticState.FrameEnd + staticState.RestPosition * staticState.FrameLen;
					}

					// reset retract motion indicator only after the rest position has been left, to avoid ball interactions
					// use a linear pullback motion
					if (movement.Position > 1.0f + staticState.FrameEnd + staticState.RestPosition * staticState.FrameLen && velocity.PullForce > 0) {
						movement.RetractMotion = false;
						movement.Speed = 3.0f * velocity.PullForce; // 3 = magic
					}
				}

			} else {
				// Not firing and not pulled by the keyboard.  Sync the on-screen
				// plunger with the physical one (or, without a mech plunger, with
				// the rest position).
				//
				// This isn't as simple as just setting the software plunger's
				// position to magically match that of the physical plunger.  If
				// we did that, we'd break the simulation by making the software
				// plunger move at infinite speed, which would prevent the
				// collision detection code from working properly.
				//
				// So instead, sync up the positions by setting the software
				// plunger in motion on a course for syncing up with the
				// physical plunger, as fast as we can while maintaining a
				// realistic speed in the simulation.
				//
				// Without an active sensor the input reads as "at rest", so the
				// plunger follows its park position.
				var mech = isMech ? input.GetPosition(staticState.RestPosition) : staticState.RestPosition;
				var target = autoPlunger ? staticState.RestPosition : mech;
				var dx = target - pos;

				// The sensor knows when the physical plunger was released: either
				// the device measures the speed, or the position dropped quickly
				// from a recent apex.  Real releases are too fast for the sampling
				// rate, so instead of following the samples we launch the virtual
				// plunger at that speed, like Fire() does for the keyboard.
				var hitVelocity = movement.SensorHitVelocity;
				if (hitVelocity < 0f && dx < 0f) {
					movement.Speed = hitVelocity * staticState.FrameLen * (staticState.SpeedFire / 100f) / Plunger.PlungerMass;

				} else {
					// Model the software plunger as though it were connected to the
					// mechanical plunger by a spring with spring constant 'mech
					// strength'.  The force from a stretched spring is -kx (spring
					// constant times displacement); in this case, the displacement
					// is the distance between the physical and virtual plunger tip
					// positions ('dx').  The force from an acceleration is ma,
					// so the acceleration from the spring force is -kx/m.  Apply
					// this acceleration to the current plunger speed.  While we're
					// at it, apply some damping to the current speed to simulate
					// friction.
					//
					// The 1/13 factor is historical; old versions applied it as an
					// empirical adjustment.  The 'dt' factor represents 1 ms in VP 9
					// physics frame units, which the other constants were chosen for.
					const float plungerFriction = 0.95f;
					const float dt = 0.1f;
					movement.Speed *= plungerFriction;
					movement.Speed += dt * velocity.MechStrength * dx * staticState.FrameLen / (Plunger.PlungerMass * 13.0f);
				}

				// add any reverse impulse to the result
				movement.Speed += movement.ReverseImpulse;
			}

			// cancel any reverse impulse
			movement.ReverseImpulse = 0.0f;
		}
	}
}
