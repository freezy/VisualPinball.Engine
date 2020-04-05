// ReSharper disable CompareOfFloatsByEqualityOperator

using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Physics;

namespace VisualPinball.Unity.VPT.Flipper
{
	[UpdateInGroup(typeof(UpdateVelocitiesSystemGroup))]
	public class FlipperVelocitySystem : JobComponentSystem
	{
		//[BurstCompile]
		private struct FlipperVelocity : IJobForEachWithEntity<FlipperMovementData, FlipperVelocityData, SolenoidStateData, FlipperMaterialData>
		{
			public void Execute(Entity entity, int index, ref FlipperMovementData mState, ref FlipperVelocityData vState, [ReadOnly] ref SolenoidStateData solenoid, [ReadOnly] ref FlipperMaterialData data)
			{
				var angleMin = math.min(data.AngleStart, data.AngleEnd);
				var angleMax = math.max(data.AngleStart, data.AngleEnd);

				var desiredTorque = data.Strength;
				if (!solenoid.Value) {
					// this.True solState = button pressed, false = released
					desiredTorque *= -data.ReturnRatio;
				}

				// hold coil is weaker
				var eosAngle = math.radians(data.TorqueDampingAngle);
				if (math.abs(mState.Angle - data.AngleEnd) < eosAngle) {
					// fade in/out damping, depending on angle to end
					var lerp = math.pow(math.abs(mState.Angle - data.AngleEnd) / eosAngle, 4);
					desiredTorque *= lerp + data.TorqueDamping * (1 - lerp);
				}

				if (!vState.Direction) {
					desiredTorque = -desiredTorque;
				}

				var torqueRampUpSpeed = data.RampUpSpeed;
				if (torqueRampUpSpeed <= 0) {
					// set very high for instant coil response
					torqueRampUpSpeed = 1e6f;

				} else {
					torqueRampUpSpeed = math.min(data.Strength / torqueRampUpSpeed, 1e6f);
				}

				// update current torque linearly towards desired torque
				// (simple model for coil hysteresis)
				if (desiredTorque >= vState.CurrentTorque) {
					vState.CurrentTorque = math.min(vState.CurrentTorque + torqueRampUpSpeed * PhysicsConstants.PhysFactor, desiredTorque);

				} else {
					vState.CurrentTorque = math.max(vState.CurrentTorque - torqueRampUpSpeed * PhysicsConstants.PhysFactor, desiredTorque);
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

				mState.AngularMomentum += PhysicsConstants.PhysFactor * torque;
				mState.AngleSpeed = mState.AngularMomentum / data.Inertia;
				vState.AngularAcceleration = torque / data.Inertia;
			}
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			return new FlipperVelocity().Schedule(this, inputDeps);
		}
	}
}
