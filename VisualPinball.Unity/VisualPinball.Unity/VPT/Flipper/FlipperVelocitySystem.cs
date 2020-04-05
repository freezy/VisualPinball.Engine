// ReSharper disable CompareOfFloatsByEqualityOperator

using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Physics;

namespace VisualPinball.Unity.VPT.Flipper
{
	[AlwaysSynchronizeSystem]
	[UpdateInGroup(typeof(VisualPinballUpdateVelocitiesSystemGroup))]
	public class FlipperVelocitySystem : JobComponentSystem
	{
		#if FLIPPER_LOG
		private VisualPinballSimulationSystemGroup _simulationSystemGroup;
		private VisualPinballSimulatePhysicsCycleSystemGroup _simulatePhysicsCycleSystemGroup;

		private long _debugRelTimeDelta = 0;

		protected override void OnCreate()
		{
			_simulationSystemGroup = World.GetOrCreateSystem<VisualPinballSimulationSystemGroup>();
			_simulatePhysicsCycleSystemGroup = World.GetOrCreateSystem<VisualPinballSimulatePhysicsCycleSystemGroup>();
		}
		#endif

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			Entities.WithoutBurst().ForEach((ref FlipperMovementData mState, ref FlipperVelocityData vState, in SolenoidStateData solenoid, in FlipperMaterialData data) => {

				#if FLIPPER_LOG
				if (_debugRelTimeDelta == 0 && mState.AngleSpeed != 0) {
					_debugRelTimeDelta = _simulationSystemGroup.CurPhysicsFrameTime;
				}
				if (mState.AngleSpeed != 0) {
					var relTime = _simulationSystemGroup.CurPhysicsFrameTime - _debugRelTimeDelta + 1000;
					VisualPinball.Unity.Game.Player.DebugLog.WriteLine($"{relTime},{-mState.AngleSpeed}");
				}
				#endif

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
			}).Run();

			return default;
		}
	}
}
