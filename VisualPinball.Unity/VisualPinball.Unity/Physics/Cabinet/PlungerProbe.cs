// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Generic;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Runs one analog plunger sensor through the engine's filter and a default
	/// virtual plunger, without a table. Used to test a device in setup screens.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The filter and the plunger follow the exact code the table physics runs.
	/// The ball is a simplified one: it sits on the tip, is carried by the plunger
	/// while the tip pushes it, and rolls back down a straight lane under gravity.
	/// It shows a launch, but not how a particular table's plunger and lane react.
	/// </para>
	/// <para>
	/// <see cref="Enqueue"/> and <see cref="ResetClock"/> may be called from any
	/// thread. <see cref="Step"/> and <see cref="Snapshot"/> belong to the owning
	/// thread.
	/// </para>
	/// </remarks>
	public sealed class PlungerProbe
	{
		/// <summary>Stroke of the virtual plunger, in VPX units.</summary>
		public const float FrameLength = 80f;

		/// <summary>Rest position of the virtual plunger, as in vpinball's plunger plot.</summary>
		public const float RestPosition = 1f / 6f;

		/// <summary>Length of the virtual shooter lane above the tip at rest, in VPX units.</summary>
		public const float LaneLength = 600f;

		private const float SpeedFire = 80f;
		private const float MechStrength = 85f;
		private const float BallLaneGravity = 0.2f; // VPX units per 10 ms per 10 ms, about a 6.5° playfield
		private const float StepTime = (float)(1000.0 / PhysicsConstants.DefaultStepTime); // one millisecond in physics frame units
		private const long MaxStepUsec = 100000;
		private const int BallTimeoutSteps = 5000;

		private readonly object _queueLock = new();
		private readonly Queue<PlungerSensorCommand> _queue = new();
		private readonly List<PlungerSensorCommand> _drain = new();

		private PlungerInputState _input;
		private PlungerStaticState _static;
		private PlungerMovementState _movement;
		private PlungerVelocityState _velocity;
		private ulong _clockUsec = 1000000;
		private long _pendingUsec;

		private float _ballY;
		private float _ballSpeed;
		private bool _ballFree;
		private int _ballFreeSteps;
		private int _launchCount;
		private float _lastLaunchSpeed;
		private int _contactCount;

		public PlungerProbe(bool velocityMapped = false)
		{
			_static = new PlungerStaticState {
				FrameStart = FrameLength,
				FrameEnd = 0f,
				FrameLen = FrameLength,
				RestPosition = RestPosition,
				SpeedFire = SpeedFire,
				IsMechPlunger = true,
				MomentumXfer = 1f
			};
			_velocity = new PlungerVelocityState { MechStrength = MechStrength };
			_input = new PlungerInputState();
			_input.ConfigureSensors(1);
			Configure(velocityMapped);
		}

		/// <summary>
		/// Launch speed of a full pull without a velocity channel, in VPX units per 10 ms.
		/// </summary>
		public static float FullPullLaunchSpeed => (1f - RestPosition) * 100f / 13f * FrameLength * (SpeedFire / 100f) / Plunger.PlungerMass;

		/// <summary>
		/// Sets which channels the sensor has. Restarts the filter and the virtual plunger.
		/// </summary>
		public void Configure(bool velocityMapped)
		{
			lock (_queueLock) {
				_queue.Clear();
			}
			_input.ConfigureSensor(0, true, velocityMapped, true);
			_movement = new PlungerMovementState {
				Position = _static.FrameEnd + RestPosition * FrameLength,
				TravelLimit = _static.FrameEnd
			};
			_velocity.AutoFireTimer = 0;
			ResetBall();
			_pendingUsec = 0;
		}

		/// <summary>
		/// Queues a mapped sample: position 0 at rest and 1 fully retracted, or velocity
		/// in position units per second.
		/// </summary>
		/// <param name="timestampUsec">Host time of the sample, as reported by native input.</param>
		public void Enqueue(PlungerSensorChannel channel, float value, long timestampUsec)
		{
			lock (_queueLock) {
				if (_queue.Count >= 4096) {
					_queue.Clear();
					_queue.Enqueue(PlungerSensorCommand.ResetClocks());
				}
				_queue.Enqueue(PlungerSensorCommand.Sample(0, channel, value, timestampUsec));
			}
		}

		/// <summary>
		/// Drops the time alignment, e.g. when switching between event and snapshot sampling.
		/// </summary>
		public void ResetClock()
		{
			lock (_queueLock) {
				_queue.Enqueue(PlungerSensorCommand.ResetClocks());
			}
		}

		/// <summary>
		/// Advances the probe by the given real time, in whole milliseconds.
		/// Longer gaps than 100 ms restart the time alignment instead of catching up.
		/// </summary>
		public void Step(long elapsedUsec)
		{
			if (elapsedUsec <= 0) {
				return;
			}
			if (elapsedUsec > MaxStepUsec) {
				_input.ResetClocks();
				elapsedUsec = MaxStepUsec;
				_pendingUsec = 0;
			}
			_pendingUsec += elapsedUsec;
			var steps = _pendingUsec / 1000;
			_pendingUsec -= steps * 1000;
			var batchEnd = _clockUsec + (ulong)(steps * 1000);

			_drain.Clear();
			lock (_queueLock) {
				_drain.AddRange(_queue);
				_queue.Clear();
			}
			foreach (var command in _drain) {
				if (command.Type == PlungerSensorCommandType.Sample) {
					_input.Stage(0, command.Channel, command.Value, command.TimestampUsec, batchEnd);
				} else if (command.Type == PlungerSensorCommandType.ResetClocks) {
					_input.ResetClocks();
				}
			}
			_input.EndStaging(batchEnd);

			for (var i = 0; i < steps; i++) {
				_clockUsec += 1000;
				_input.StepOneMillisecond(_clockUsec);
				PlungerVelocityPhysics.UpdateVelocities(ref _movement, ref _velocity, in _static, in _input);
				MovePlunger();
				MoveBall();
			}
		}

		public PlungerProbeSnapshot Snapshot()
		{
			var sensor = _input.GetSensor(0);
			return new PlungerProbeSnapshot(
				_clockUsec,
				sensor.IsActive,
				sensor.HasPositionReading,
				sensor.PositionReading,
				sensor.IsVelocityMapped,
				sensor.VelocityReading,
				_input.RawPosition,
				_input.RawVelocity,
				_input.GetHitVelocity(RestPosition),
				(_movement.Position - _static.FrameEnd) / FrameLength,
				_movement.Speed,
				_ballY,
				_ballSpeed,
				_launchCount,
				_lastLaunchSpeed,
				_contactCount
			);
		}

		// Same travel and range rules as PlungerDisplacementPhysics; this plunger never fires on its own.
		private void MovePlunger()
		{
			_movement.Position += StepTime * _movement.Speed;
			if (_movement.Position < _static.FrameEnd) {
				_movement.Speed = 0f;
				_movement.Position = _static.FrameEnd;
			} else if (_movement.Position > _static.FrameStart) {
				_movement.Speed = 0f;
				_movement.Position = _static.FrameStart;
			}
		}

		// Lane coordinate of the tip: 0 at rest, positive towards the playfield.
		private float TipY() => (RestPosition * FrameLength) - (_movement.Position - _static.FrameEnd);

		private void MoveBall()
		{
			var tipY = TipY();
			var tipSpeed = -_movement.Speed;
			if (!_ballFree) {
				// Resting on the tip: pushed along, and it leaves once the tip slows
				// down faster than gravity slows the ball.
				var alone = _ballSpeed - BallLaneGravity * StepTime;
				if (alone > 0f && alone > tipSpeed) {
					Release(alone);
				} else {
					_ballSpeed = tipSpeed;
					_ballY = tipY;
					return;
				}
			}

			_ballFreeSteps++;
			_ballSpeed -= BallLaneGravity * StepTime;
			_ballY += StepTime * _ballSpeed;
			if (_ballY <= tipY) {
				// Back on the tip.
				_contactCount++;
				ResetBall();
				return;
			}
			if (_ballY > LaneLength || _ballFreeSteps > BallTimeoutSteps) {
				ResetBall();
			}
		}

		private void Release(float speed)
		{
			_ballFree = true;
			_ballFreeSteps = 0;
			_ballSpeed = speed;
			_contactCount++;
			if (speed > 0.5f) {
				_launchCount++;
				_lastLaunchSpeed = speed;
			}
		}

		private void ResetBall()
		{
			_ballFree = false;
			_ballY = TipY();
			_ballSpeed = 0f;
			_ballFreeSteps = 0;
		}
	}

	/// <summary>
	/// One reading of a <see cref="PlungerProbe"/>.
	/// </summary>
	public readonly struct PlungerProbeSnapshot
	{
		public PlungerProbeSnapshot(ulong timeUsec, bool active, bool hasPositionReading, float positionReading, bool velocityMapped,
			float velocityReading, float filteredPosition, float filteredVelocity, float hitVelocity, float plungerPosition,
			float plungerSpeed, float ballPosition, float ballSpeed, int launchCount, float lastLaunchSpeed, int contactCount)
		{
			TimeUsec = timeUsec;
			IsActive = active;
			HasPositionReading = hasPositionReading;
			PositionReading = positionReading;
			IsVelocityMapped = velocityMapped;
			VelocityReading = velocityReading;
			FilteredPosition = filteredPosition;
			FilteredVelocity = filteredVelocity;
			HitVelocity = hitVelocity;
			PlungerPosition = plungerPosition;
			PlungerSpeed = plungerSpeed;
			BallPosition = ballPosition;
			BallSpeed = ballSpeed;
			LaunchCount = launchCount;
			LastLaunchSpeed = lastLaunchSpeed;
			ContactCount = contactCount;
		}

		/// <summary>Probe clock, in microseconds.</summary>
		public ulong TimeUsec { get; }

		/// <summary>The sensor moved recently. Inactive sensors read as "at rest".</summary>
		public bool IsActive { get; }

		public bool HasPositionReading { get; }

		/// <summary>Latest mapped position sample: 0 at rest, 1 fully retracted.</summary>
		public float PositionReading { get; }

		public bool IsVelocityMapped { get; }

		/// <summary>Latest mapped velocity sample, in position units per second.</summary>
		public float VelocityReading { get; }

		/// <summary>Filtered position: 0 at rest, 1 fully retracted.</summary>
		public float FilteredPosition { get; }

		/// <summary>Filtered velocity, in position units per second.</summary>
		public float FilteredVelocity { get; }

		/// <summary>Release speed the plunger physics reads, in travel units per second, negative towards the ball.</summary>
		public float HitVelocity { get; }

		/// <summary>Virtual plunger: 0 fully forward, 1 fully retracted.</summary>
		public float PlungerPosition { get; }

		/// <summary>Virtual plunger speed, in VPX units per 10 ms, negative towards the ball.</summary>
		public float PlungerSpeed { get; }

		/// <summary>Ball position along the lane, in VPX units: 0 on the tip at rest, positive towards the playfield.</summary>
		public float BallPosition { get; }

		/// <summary>Ball speed along the lane, in VPX units per 10 ms.</summary>
		public float BallSpeed { get; }

		/// <summary>Number of launches so far.</summary>
		public int LaunchCount { get; }

		/// <summary>Speed of the last launch, in VPX units per 10 ms.</summary>
		public float LastLaunchSpeed { get; }

		/// <summary>Number of times the ball left or landed on the tip.</summary>
		public int ContactCount { get; }

		/// <summary>Last launch relative to a full pull without a velocity channel.</summary>
		public float LastLaunchFraction => PlungerProbe.FullPullLaunchSpeed > 0f ? LastLaunchSpeed / PlungerProbe.FullPullLaunchSpeed : 0f;
	}
}
