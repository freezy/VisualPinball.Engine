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
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// Covers how the table plunger consumes the analog plunger input, and the
	/// synthetic Launch Ball press of auto plungers.
	/// </summary>
	public class PlungerSensorPhysicsTests
	{
		private const float FrameLen = 80f;
		private const float RestPosition = 0.25f;
		private const float SpeedFire = 80f;

		private static PlungerStaticState Static(bool mech, bool auto = false) => new() {
			FrameEnd = 0f,
			FrameStart = FrameLen,
			FrameLen = FrameLen,
			RestPosition = RestPosition,
			IsMechPlunger = mech,
			IsAutoPlunger = auto,
			SpeedFire = SpeedFire,
			MomentumXfer = 1f
		};

		private static PlungerMovementState AtPosition(float tu) => new() {
			Position = tu * FrameLen,
			TravelLimit = 0f
		};

		private static PlungerVelocityState Velocity() => new() { MechStrength = 85f };

		private static PlungerInputState External(float position, float velocity)
		{
			var input = new PlungerInputState();
			input.SetExternalPlunger(true, velocity, position);
			return input;
		}

		[Test]
		public void DefaultInputHasNoSource()
		{
			var input = new PlungerInputState();
			Assert.That(input.HasSensor, Is.False);
			Assert.That(input.ActiveSourceIndex, Is.EqualTo(PlungerInputState.NoSource));
		}

		[Test]
		public void MechPlungerIsPulledTowardsTheSensorPosition()
		{
			var staticState = Static(mech: true);
			var movement = AtPosition(RestPosition);
			var velocity = Velocity();
			var input = External(1f, 0f);

			PlungerVelocityPhysics.UpdateVelocities(ref movement, ref velocity, in staticState, in input);

			Assert.That(movement.HasSensor, Is.True);
			Assert.That(movement.Speed, Is.GreaterThan(0f));
		}

		[Test]
		public void ReleaseLaunchesAtTheHitVelocity()
		{
			var staticState = Static(mech: true);
			var movement = AtPosition(1f);
			var velocity = Velocity();
			var input = External(0f, -5f);

			PlungerVelocityPhysics.UpdateVelocities(ref movement, ref velocity, in staticState, in input);

			var hitVelocity = -5f * (1f - RestPosition);
			Assert.That(movement.SensorHitVelocity, Is.EqualTo(hitVelocity).Within(1e-5f));
			Assert.That(movement.Speed, Is.EqualTo(hitVelocity * FrameLen * (SpeedFire / 100f) / Plunger.PlungerMass).Within(1e-4f));
		}

		[Test]
		public void NonMechPlungerIgnoresTheSensor()
		{
			var staticState = Static(mech: false);
			var movement = AtPosition(RestPosition);
			var velocity = Velocity();
			var input = External(1f, -5f);

			PlungerVelocityPhysics.UpdateVelocities(ref movement, ref velocity, in staticState, in input);

			Assert.That(movement.HasSensor, Is.False);
			Assert.That(movement.SensorHitVelocity, Is.EqualTo(0f));
			Assert.That(movement.Speed, Is.EqualTo(0f).Within(1e-6f));
		}

		[Test]
		public void AutoPlungerReleaseHoldsLaunchBallForOneHundredMilliseconds()
		{
			var staticState = Static(mech: true, auto: true);
			var movement = AtPosition(RestPosition);
			var velocity = Velocity();
			// -20 p.u./s on a 3" plunger is about -1.5 m/s.
			var input = External(0f, -20f);

			PlungerVelocityPhysics.UpdateVelocities(ref movement, ref velocity, in staticState, in input);
			Assert.That(velocity.AutoFireTimer, Is.EqualTo(101));
			Assert.That(velocity.AutoFireCount, Is.EqualTo(1));

			input = External(0f, 0f);
			for (var i = 0; i < 101; i++) {
				PlungerVelocityPhysics.UpdateVelocities(ref movement, ref velocity, in staticState, in input);
			}
			Assert.That(velocity.AutoFireTimer, Is.EqualTo(0));
			Assert.That(velocity.AutoFireCount, Is.EqualTo(1));
		}

		[Test]
		public void FiringTheAutoPlungerDoesNotExtendLaunchBall()
		{
			var staticState = Static(mech: true, auto: true);
			var movement = AtPosition(RestPosition);
			var velocity = Velocity();
			var input = External(0f, -20f);
			PlungerVelocityPhysics.UpdateVelocities(ref movement, ref velocity, in staticState, in input);

			// The table answers Launch Ball by firing the plunger.
			movement.FireTimer = 200;
			movement.FireSpeed = -10f;
			input = External(0f, 0f);
			for (var i = 0; i < 101; i++) {
				PlungerVelocityPhysics.UpdateVelocities(ref movement, ref velocity, in staticState, in input);
			}
			Assert.That(velocity.AutoFireTimer, Is.EqualTo(0));
			Assert.That(movement.FireTimer, Is.GreaterThan(0));
		}

		[Test]
		public void SlowAutoPlungerMotionDoesNotLaunch()
		{
			var staticState = Static(mech: true, auto: true);
			var movement = AtPosition(RestPosition);
			var velocity = Velocity();
			var input = External(0.5f, -10f);

			PlungerVelocityPhysics.UpdateVelocities(ref movement, ref velocity, in staticState, in input);
			Assert.That(velocity.AutoFireTimer, Is.EqualTo(0));
		}

		[Test]
		public void PullBackAndRetractOnlyRetractsRegularPlungers()
		{
			var velocity = Velocity();
			var movement = AtPosition(RestPosition);
			PlungerCommands.PullBackAndRetract(5f, false, ref velocity, ref movement);
			Assert.That(velocity.AddRetractMotion, Is.True);

			PlungerCommands.PullBackAndRetract(5f, true, ref velocity, ref movement);
			Assert.That(velocity.AddRetractMotion, Is.False);
		}

		[Test]
		public void LaunchTrackerReportsHoldEdgesAndPulses()
		{
			var tracker = new AutoPlungerLaunchTracker();
			Assert.That(tracker.Update(3, false), Is.EqualTo(AutoPlungerLaunchTracker.Change.None));
			Assert.That(tracker.Update(4, true), Is.EqualTo(AutoPlungerLaunchTracker.Change.Press));
			Assert.That(tracker.Update(4, true), Is.EqualTo(AutoPlungerLaunchTracker.Change.None));
			Assert.That(tracker.Update(4, false), Is.EqualTo(AutoPlungerLaunchTracker.Change.Release));
			// A whole press between two reads.
			Assert.That(tracker.Update(5, false), Is.EqualTo(AutoPlungerLaunchTracker.Change.Pulse));
			Assert.That(tracker.Update(5, false), Is.EqualTo(AutoPlungerLaunchTracker.Change.None));
		}

		[Test]
		public void HoldAggregatorKeepsThePhysicalButtonHeld()
		{
			var holds = new InputActionHoldAggregator();
			const string action = InputConstants.ActionPlunger;

			Assert.That(holds.SetPhysical(action, true, out var effective), Is.True);
			Assert.That(effective, Is.True);
			Assert.That(holds.SetSynthetic(action, true, out _), Is.False);
			Assert.That(holds.SetSynthetic(action, false, out _), Is.False);
			Assert.That(holds.IsPressed(action), Is.True);
			Assert.That(holds.SetPhysical(action, false, out effective), Is.True);
			Assert.That(effective, Is.False);

			Assert.That(holds.SetSynthetic(action, true, out effective), Is.True);
			Assert.That(effective, Is.True);
			Assert.That(holds.SetPhysical(action, true, out _), Is.False);
			Assert.That(holds.SetPhysical(action, false, out _), Is.False);
			Assert.That(holds.SetSynthetic(action, false, out effective), Is.True);
			Assert.That(effective, Is.False);
		}
	}
}
