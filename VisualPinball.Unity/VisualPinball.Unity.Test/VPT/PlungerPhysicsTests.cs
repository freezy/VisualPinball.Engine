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

using FluentAssertions;
using NUnit.Framework;

namespace VisualPinball.Unity.Test
{
	public class PlungerPhysicsTests
	{
		[Test]
		public void ShouldFireProportionallyToPullDistance()
		{
			var staticState = CreateStaticState();

			var rest = FireFrom(0.0f, staticState);
			var half = FireFrom(0.5f, staticState);
			var full = FireFrom(1.0f, staticState);

			rest.FireSpeed.Should().BeApproximately(0.0f, 0.0001f);
			half.FireSpeed.Should().BeLessThan(0.0f);
			full.FireSpeed.Should().BeLessThan(half.FireSpeed);

			var halfDistance = 0.5f - staticState.RestPosition;
			var fullDistance = 1.0f - staticState.RestPosition;
			half.FireSpeed.Should().BeApproximately(full.FireSpeed * halfDistance / fullDistance, 0.0001f);
		}

		[Test]
		[Explicit("Known failing characterization for Phase 1: non-mechanical plungers currently still apply MechStrength while idle.")]
		public void ShouldNotApplyMechStrengthToNonMechanicalPlunger()
		{
			var staticState = CreateStaticState();
			staticState.IsMechPlunger = false;
			var movementWithNoStrength = CreateMovement(staticState);
			var movementWithStrength = CreateMovement(staticState);
			movementWithNoStrength.AnalogPosition = 1.0f;
			movementWithStrength.AnalogPosition = 1.0f;

			var velocityWithNoStrength = new PlungerVelocityState { MechStrength = 0.0f };
			var velocityWithStrength = new PlungerVelocityState { MechStrength = 100.0f };

			PlungerVelocityPhysics.UpdateVelocities(ref movementWithNoStrength, ref velocityWithNoStrength, in staticState);
			PlungerVelocityPhysics.UpdateVelocities(ref movementWithStrength, ref velocityWithStrength, in staticState);

			movementWithStrength.Speed.Should().BeApproximately(movementWithNoStrength.Speed, 0.0001f);
			movementWithStrength.Position.Should().BeApproximately(movementWithNoStrength.Position, 0.0001f);
		}

		[Test]
		[Explicit("Known failing characterization for Phase 1: PullBackAndRetract currently disables the retract branch.")]
		public void ShouldEnableRetractMotionForNormalPlungerPullBackAndRetract()
		{
			var movement = new PlungerMovementState { RetractMotion = true };
			var velocity = new PlungerVelocityState();

			PlungerCommands.PullBackAndRetract(3.0f, ref velocity, ref movement);

			velocity.AddRetractMotion.Should().BeTrue();
			velocity.InitialSpeed.Should().Be(3.0f);
			movement.RetractMotion.Should().BeFalse();
		}

		[Test]
		[Explicit("Known failing characterization for Phase 1: non-mechanical plungers currently follow idle MechStrength toward rest.")]
		public void ShouldGateAnalogPositionToMechanicalPlungers()
		{
			var staticState = CreateStaticState();
			staticState.IsMechPlunger = true;
			var mechanicalMovement = CreateMovement(staticState);
			mechanicalMovement.AnalogPosition = 0.75f;
			var mechanicalVelocity = new PlungerVelocityState { MechStrength = 100.0f };

			var nonMechanicalState = staticState;
			nonMechanicalState.IsMechPlunger = false;
			var nonMechanicalMovement = CreateMovement(nonMechanicalState);
			nonMechanicalMovement.AnalogPosition = 0.75f;
			var nonMechanicalVelocity = new PlungerVelocityState { MechStrength = 100.0f };

			PlungerVelocityPhysics.UpdateVelocities(ref mechanicalMovement, ref mechanicalVelocity, in staticState);
			PlungerVelocityPhysics.UpdateVelocities(ref nonMechanicalMovement, ref nonMechanicalVelocity, in nonMechanicalState);

			mechanicalMovement.Speed.Should().NotBeApproximately(nonMechanicalMovement.Speed, 0.0001f);
			nonMechanicalMovement.Speed.Should().BeApproximately(0.0f, 0.0001f);
		}

		private static PlungerMovementState FireFrom(float startPos, PlungerStaticState staticState)
		{
			var movement = CreateMovement(staticState);
			var velocity = new PlungerVelocityState();

			PlungerCommands.Fire(startPos, ref velocity, ref movement, in staticState);
			return movement;
		}

		private static PlungerMovementState CreateMovement(PlungerStaticState staticState)
		{
			return new PlungerMovementState {
				Position = staticState.FrameEnd + staticState.RestPosition * staticState.FrameLen,
				TravelLimit = staticState.FrameEnd
			};
		}

		private static PlungerStaticState CreateStaticState()
		{
			return new PlungerStaticState {
				FrameStart = 0.0f,
				FrameEnd = -80.0f,
				FrameLen = 80.0f,
				RestPosition = 1.0f / 6.0f,
				SpeedFire = 80.0f,
				IsMechPlunger = false
			};
		}
	}
}
