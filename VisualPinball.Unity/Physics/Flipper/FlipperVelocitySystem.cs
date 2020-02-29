using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Systems;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Physics.Flipper
{
	[UpdateBefore(typeof(FlipperDisplacementSystem))]
	public class FlipperVelocitySystem : JobComponentSystem
	{
		//[BurstCompile]
		private struct FlipperVelocity : IJobForEach<FlipperMovementData, FlipperVelocityData, SolenoidStateData, FlipperMaterialData>
		{
			public float DTime;

			public void Execute(ref FlipperMovementData mState, ref FlipperVelocityData vState, [ReadOnly] ref SolenoidStateData solenoid, [ReadOnly] ref FlipperMaterialData data)
			{
				var dTime = (int) (DTime * 1000000);
				var curPhysicsFrameTime = 0;
				while (curPhysicsFrameTime <= dTime) {

					var desiredTorque = data.Strength;
					if (!solenoid.Value) {
						// this.True solState = button pressed, false = released
						desiredTorque *= -data.ReturnRatio;
					}

					// hold coil is weaker
					var eosAngle = math.radians(data.TorqueDampingAngle);
					if (math.abs(mState.Angle - data.AngleEnd) < eosAngle) {
						// fade in/out damping, depending on angle to end
						var lerp = math.sqrt(math.sqrt(math.abs(mState.Angle - data.AngleEnd) / eosAngle));
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
						var angleMin = math.min(data.AngleStart, data.AngleEnd);
						var angleMax = math.max(data.AngleStart, data.AngleEnd);

						if (mState.Angle >= angleMax - 1e-2 && torque > 0) {
							mState.Angle = angleMax;
							vState.IsInContact = true;
							vState.ContactTorque = torque;
							mState.AngularMomentum = 0;
							torque = 0;

						} else if (mState.Angle <= angleMin + 1e-2 && torque < 0) {
							mState.Angle = angleMin;
							vState.IsInContact = true;
							vState.ContactTorque = torque;
							mState.AngularMomentum = 0;
							torque = 0;
						}
					}

					mState.AngularMomentum += PhysicsConstants.PhysFactor * torque;
					mState.AngleSpeed = mState.AngularMomentum / data.Inertia;
					vState.AngularAcceleration = torque / data.Inertia;

					curPhysicsFrameTime += PhysicsConstants.PhysicsStepTime;
				}
			}
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var flipperVelocityJob = new FlipperVelocity {
				DTime = Time.DeltaTime
			};
			return flipperVelocityJob.Schedule(this, inputDeps);
		}
	}
}
