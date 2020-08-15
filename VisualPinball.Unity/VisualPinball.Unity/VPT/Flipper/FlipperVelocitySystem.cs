// ReSharper disable CompareOfFloatsByEqualityOperator

using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	[AlwaysSynchronizeSystem]
	[UpdateInGroup(typeof(UpdateVelocitiesSystemGroup))]
	public class FlipperVelocitySystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("FlipperVelocitySystem");

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			Entities.WithName("FlipperVelocityJob").ForEach((ref FlipperMovementData mState, ref FlipperVelocityData vState, in SolenoidStateData solenoid, in FlipperStaticData data) => {

				marker.Begin();

				var angleMin = math.min(data.AngleStart, data.AngleEnd);
				var angleMax = math.max(data.AngleStart, data.AngleEnd);

				var desiredTorque = data.Strength;
				if (!solenoid.Value) {
					// True solState = button pressed, false = released
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

				marker.End();

			}).Run();
		}
	}
}
