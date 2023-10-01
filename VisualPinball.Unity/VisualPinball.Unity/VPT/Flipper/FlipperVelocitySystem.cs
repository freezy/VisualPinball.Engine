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

// ReSharper disable CompareOfFloatsByEqualityOperator

using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using VisualPinball.Engine.Common;
using VisualPinball.Unity;
using VisualPinball.Unity.VisualPinball.Unity.Game;

namespace VisualPinballUnity
{
	[AlwaysSynchronizeSystem]
	[UpdateInGroup(typeof(UpdateVelocitiesSystemGroup))]
	[Obsolete("Use FlipperVelocityPhysics instead.")]
	internal partial class FlipperVelocitySystem : SystemBaseStub
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("FlipperVelocitySystem");

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			Entities.WithName("FlipperVelocityJob").ForEach((ref FlipperMovementData mState, ref FlipperVelocityData vState, ref FlipperTricksData tricks, in SolenoidStateData solenoid, in FlipperStaticData data) =>
			{

				marker.Begin();

				var angleMin = math.min(data.AngleStart, tricks.AngleEnd);
				var angleMax = math.max(data.AngleStart, tricks.AngleEnd);
				var minIsStart = angleMin == data.AngleStart; // Usually true for the right Flipper

				var desiredTorque = data.Strength;
				if (!solenoid.Value)
				{
					// True solState = button pressed, false = released
					desiredTorque *= -data.ReturnRatio;
				}

				if (tricks.UseFlipperTricksPhysics) { 
					// check if solenoid was just activated or deactivated
					if (solenoid.Value != tricks.lastSolState)
					{
						// check if solenoid was just activated or deactivated for Flippertricks
						// Flippertricks case 1 and 2 are always before case 3, 4 and 5.
						if (solenoid.Value)
						{
							// Flippertricks, case 2 (OnButtonActivate)
							tricks.TorqueDamping = tricks.OriginalTorqueDamping;
							tricks.TorqueDampingAngle = tricks.OriginalTorqueDampingAngle;
							tricks.ElasticityMultiplier = 1f;
						}
						else
						{
							// Flippertricks, case 1 (OnButtonDeactivate)
							tricks.TorqueDamping = tricks.OriginalTorqueDamping * tricks.EOSReturn / data.ReturnRatio;
							tricks.TorqueDampingAngle = tricks.OriginalTorqueDampingAngle;
						}
					}
				}

				// hold coil is weaker
				float eosAngle = math.radians(tricks.TorqueDampingAngle);
				if (math.abs(mState.Angle - tricks.AngleEnd) < eosAngle) {
					// fade in/out damping, depending on angle to end
					var lerp = math.pow(math.abs(mState.Angle - tricks.AngleEnd) / eosAngle, 4);
					if (tricks.UseFlipperTricksPhysics)
						desiredTorque *= lerp + tricks.TorqueDamping * (1 - lerp);
					else 
						desiredTorque *= lerp + tricks.TorqueDamping * (1 - lerp);
				}

				if (!vState.Direction) {
					desiredTorque = -desiredTorque;
				}

				var torqueRampUpSpeed = tricks.RampUpSpeed;
				if (torqueRampUpSpeed <= 0) {
					// set very high for instant coil response
					torqueRampUpSpeed = 1e6f;

				} else {
					torqueRampUpSpeed = math.min(data.Strength / torqueRampUpSpeed, 1e6f);
				}

				// update current torque linearly towards desired torque
				// (simple model for coil hysteresis)
				if (desiredTorque >= vState.CurrentTorque) {
					vState.CurrentTorque = math.min(vState.CurrentTorque + torqueRampUpSpeed * (float)PhysicsConstants.PhysFactor, desiredTorque);

				} else {
					vState.CurrentTorque = math.max(vState.CurrentTorque - torqueRampUpSpeed * (float)PhysicsConstants.PhysFactor, desiredTorque);
				}

				// resolve contacts with stoppers
				var torque = vState.CurrentTorque;
				vState.IsInContact = false;
				if (math.abs(mState.AngleSpeed) <= 1e-2) {

					if (mState.Angle >= angleMax - 1e-2 && torque > 0) {
						mState.Angle = angleMax;
						vState.IsInContact = true;
						vState.ContactTorque = torque;
						mState.AngularMomentum = 0f;
						torque = 0f;

					} else if (mState.Angle <= angleMin + 1e-2 && torque < 0) {
						mState.Angle = angleMin;
						vState.IsInContact = true;
						vState.ContactTorque = torque;
						mState.AngularMomentum = 0f;
						torque = 0f;
					}
				}

				mState.AngularMomentum += (float)PhysicsConstants.PhysFactor * torque;
				mState.AngleSpeed = mState.AngularMomentum / data.Inertia;
				vState.AngularAcceleration = torque / data.Inertia;

				if (tricks.UseFlipperTricksPhysics)
				{
					// Flippertricks, case 3 (OnFlipperDown) and 4 (OnFlipperUpResting)
					if (!tricks.WasInContact && vState.IsInContact)
					{
						// the flipper stopped due to being at max or min angle.
						// so check if at start angle 
						if (((mState.Angle == angleMin) && (minIsStart)) ||
							((mState.Angle == angleMax) && (!minIsStart)))
						{
							// is at start angle
							// FlipperTricks case 3: OnFlipperDown
							if (minIsStart)
								tricks.AngleEnd = tricks.OriginalAngleEnd + tricks.Overshoot;
							else
								tricks.AngleEnd = tricks.OriginalAngleEnd - tricks.Overshoot;

							tricks.RampUpSpeed = tricks.SOSRampUp;
							tricks.ElasticityMultiplier = tricks.SOSEM;

							//data.ft = 3f;
						}
						else
						{
							// is at end angle
							// FlipperTricks case 4: OnFlipperUpResting
							tricks.AngleEnd = tricks.OriginalAngleEnd; // This causes the flipper to instantly flip back to normal end angle (like in the original Flippertricks implementation)
							tricks.RampUpSpeed = tricks.EOSRampup;
							tricks.TorqueDamping = tricks.EOSTNew;
							tricks.TorqueDampingAngle = tricks.EOSANew;

							//data.ft = 4f;
						}
					}

					// Flippertricks, case 5 (OnEnterinbetween) (and pressed)
					if ((tricks.WasInContact) && (!vState.IsInContact) && solenoid.Value)
					{
						// Flippertricks Case 5
						tricks.RampUpSpeed = tricks.OriginalRampUpSpeed;
						tricks.TorqueDamping = tricks.OriginalTorqueDamping;
						tricks.TorqueDampingAngle = tricks.OriginalTorqueDampingAngle;
						tricks.ElasticityMultiplier = 1f;

						//data.ft = 5f;
					}

					tricks.WasInContact = vState.IsInContact;

					// check if solenoid was just activated or deactivated
					tricks.lastSolState = solenoid.Value;
				}
				marker.End();

			}).Run();
		}
	}
}
