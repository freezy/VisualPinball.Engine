// ReSharper disable CompareOfFloatsByEqualityOperator

using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.VPT.Flipper
{
	public struct FlipperRotatedEvent
	{
		public bool Direction;
		public float AngleSpeed;
		public int EntityIndex;
	}

	[UpdateBefore(typeof(FlipperDisplacementSystem))]
	public class FlipperVelocitySystem : JobComponentSystem
	{

		// https://www.youtube.com/watch?v=fkJ-7pqnRGo
		public event EventHandler<FlipperRotatedEvent> OnRotated;

		private NativeQueue<FlipperRotatedEvent> _eventQueue;

		//[BurstCompile]
		private struct FlipperVelocity : IJobForEachWithEntity<FlipperMovementData, FlipperVelocityData, SolenoidStateData, FlipperMaterialData, PhysicsVelocity>
		{
			public float DTime;
			public double ElapsedTime;
			public NativeQueue<FlipperRotatedEvent>.ParallelWriter EventQueue;

			public void Execute(Entity entity, int index, ref FlipperMovementData mState, ref FlipperVelocityData vState, [ReadOnly] ref SolenoidStateData solenoid, [ReadOnly] ref FlipperMaterialData data, ref PhysicsVelocity pv)
			{
				float dir = data.AngleStart < data.AngleEnd ? 1 : -1;
				if (solenoid.Value)
				{
					pv.Angular = new float3(0, 0, 900*dir*DTime);
				} else
				{
					pv.Angular = new float3(0, 0, -500*dir* DTime);
				}
				return;
				var initialTimeUsec = (long) (ElapsedTime * 1000000);
				var curPhysicsFrameTime = mState.CurrentPhysicsTime == 0
					? (long) (initialTimeUsec - DTime * 1000000)
					: mState.CurrentPhysicsTime;
				var nextPhysicsFrameTime = curPhysicsFrameTime + PhysicsConstants.PhysicsStepTime;
				var moving = mState.AngleSpeed != 0;
				var jobStart = true;
				var angleMin = math.min(data.AngleStart, data.AngleEnd);
				var angleMax = math.max(data.AngleStart, data.AngleEnd);

				//TablePlayer.DebugLog.WriteLine($"-- initial: {initialTimeUsec}, cur: {curPhysicsFrameTime}, cur-last: {mState.CurrentPhysicsTime}, elapsed: {(long)(ElapsedTime * 1000000)}, dt: {(long)(DTime * 1000000)}");

				while (curPhysicsFrameTime < initialTimeUsec) {

					// todo remove debug log
					// if (mState.AngleSpeed != 0) {
					// 	if (!moving) {
					// 		mState.DebugRelTimeDelta = curPhysicsFrameTime;
					// 		moving = true;
					// 	}
					// 	var relTime = curPhysicsFrameTime - mState.DebugRelTimeDelta + 1000;
					// 	var mt = jobStart ? mState.CurrentPhysicsTime.ToString() : "";
					// 	var js = jobStart ? ((int)(DTime * 1000000)).ToString() : "";
					// 	TablePlayer.DebugLog.WriteLine($"{relTime},{-mState.AngleSpeed},{js},{mt}");
					// 	jobStart = false;
					// }

					#region UpdateVelocities

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

					#endregion

					#region Displacement

					var physicsDiffTime = (float)((nextPhysicsFrameTime - curPhysicsFrameTime)*(1.0 / 10000));
					mState.Angle += mState.AngleSpeed * physicsDiffTime; // move flipper angle

					if (mState.Angle > angleMax) {
						mState.Angle = angleMax;
					}

					if (mState.Angle < angleMin) {
						mState.Angle = angleMin;
					}

					if (math.abs(mState.AngleSpeed) < 0.0005f) {
						// avoids "jumping balls" when two or more balls held on flipper (and more other balls are in play) //!! make dependent on physics update rate
						curPhysicsFrameTime = nextPhysicsFrameTime;
						nextPhysicsFrameTime += PhysicsConstants.PhysicsStepTime;
						continue;
					}

					var comeToStop = false;

					if (mState.Angle == angleMax) {
						// hit stop?
						if (mState.AngleSpeed > 0) {
							comeToStop = true;
						}

					} else if (mState.Angle == angleMin) {
						if (mState.AngleSpeed < 0) {
							comeToStop = true;
						}
					}

					if (comeToStop) {
						mState.AngularMomentum *= -0.3f; // make configurable?
						mState.AngleSpeed = mState.AngularMomentum / data.Inertia;

						var eventAngleSpeed = math.abs(math.degrees(mState.AngleSpeed));
						if (mState.EnableRotateEvent > 0) {
							// eos
							EventQueue.Enqueue(new FlipperRotatedEvent { AngleSpeed = eventAngleSpeed, Direction = false, EntityIndex = entity.Index});

						} else if (mState.EnableRotateEvent < 0) {
							// bos
							EventQueue.Enqueue(new FlipperRotatedEvent { AngleSpeed = eventAngleSpeed, Direction = true, EntityIndex = entity.Index });
						}
						mState.EnableRotateEvent = 0;
					}

					#endregion

					curPhysicsFrameTime = nextPhysicsFrameTime;                   // new cycle, on physics frame boundary
					nextPhysicsFrameTime += PhysicsConstants.PhysicsStepTime;     // advance physics position
				}

				mState.CurrentPhysicsTime = curPhysicsFrameTime;
			}
		}

		protected override void OnCreate()
		{
			_eventQueue = new NativeQueue<FlipperRotatedEvent>(Allocator.Persistent);
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var flipperVelocityJob = new FlipperVelocity {
				DTime = Time.DeltaTime,
				ElapsedTime = Time.ElapsedTime,
				EventQueue = _eventQueue.AsParallelWriter()
			};

			var deps = flipperVelocityJob.Schedule(this, inputDeps);
			deps.Complete();

			while (_eventQueue.TryDequeue(out var flipperRotatedEvent)) {
				OnRotated?.Invoke(this, flipperRotatedEvent);
			}

			return deps;
		}

		protected override void OnDestroy()
		{
			_eventQueue.Dispose();
		}
	}
}
