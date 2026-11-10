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

namespace VisualPinball.Unity
{
	internal enum PlungerSensorCommandType : byte
	{
		/// <summary>One mapped reading.</summary>
		Sample,
		/// <summary>Drop staged samples and filter state of all sensors, keep activation.</summary>
		ResetClocks,
		/// <summary>Deactivate one sensor (device gone) or bring it back.</summary>
		SetAvailable
	}

	/// <summary>
	/// One entry of the queue between the input thread and the physics owner.
	/// </summary>
	/// <remarks>
	/// Resets travel in the same queue as samples, so a reset can never overtake
	/// samples that were taken before it.
	/// </remarks>
	internal readonly struct PlungerSensorCommand
	{
		public readonly PlungerSensorCommandType Type;
		public readonly int SensorIndex;
		public readonly PlungerSensorChannel Channel;
		public readonly float Value;
		public readonly long TimestampUsec;
		public readonly bool Available;

		private PlungerSensorCommand(PlungerSensorCommandType type, int sensorIndex, PlungerSensorChannel channel,
			float value, long timestampUsec, bool available)
		{
			Type = type;
			SensorIndex = sensorIndex;
			Channel = channel;
			Value = value;
			TimestampUsec = timestampUsec;
			Available = available;
		}

		public static PlungerSensorCommand Sample(int sensorIndex, PlungerSensorChannel channel, float value, long timestampUsec)
			=> new(PlungerSensorCommandType.Sample, sensorIndex, channel, value, timestampUsec, true);

		public static PlungerSensorCommand ResetClocks()
			=> new(PlungerSensorCommandType.ResetClocks, -1, PlungerSensorChannel.Position, 0f, 0, true);

		public static PlungerSensorCommand SetAvailable(int sensorIndex, bool available)
			=> new(PlungerSensorCommandType.SetAvailable, sensorIndex, PlungerSensorChannel.Position, 0f, 0, available);
	}

	/// <summary>
	/// Main-thread readable snapshot of the plunger input, for UI and diagnostics.
	/// </summary>
	public struct PlungerInputTelemetry
	{
		public struct Sensor
		{
			public bool Enabled;
			public bool PositionMapped;
			public bool VelocityMapped;
			public bool Active;
			public bool HasPositionReading;
			/// <summary>Latest position reading, mapped, unfiltered, in p.u.</summary>
			public float PositionReading;
			/// <summary>Latest velocity reading, mapped, in p.u./s.</summary>
			public float VelocityReading;
			/// <summary>Filtered position in p.u.</summary>
			public float Position;
			/// <summary>Filtered velocity in p.u./s.</summary>
			public float Velocity;
			/// <summary>Highest filtered position of the last 100 ms, in p.u.</summary>
			public float ReleaseApex;
			/// <summary>Hit velocity for a plunger resting at 0 t.u., in t.u./s.</summary>
			public float HitVelocity;
			public int OverflowCount;
		}

		public int SensorCount;
		/// <summary>Active sensor index, <see cref="PlungerInputState.NoSource"/> or <see cref="PlungerInputState.ExternalSource"/>.</summary>
		public int ActiveSourceIndex;
		public float Position;
		public float Velocity;
		public Sensor Sensor0;
		public Sensor Sensor1;
		public Sensor Sensor2;
		public Sensor Sensor3;
		/// <summary>Sum of ball contacts of all plungers, increments on every contact.</summary>
		public int BallContactCount;
		/// <summary>Number of synthetic Launch Ball presses triggered by auto plungers.</summary>
		public int AutoFireCount;

		public Sensor GetSensor(int index) => index switch {
			0 => Sensor0,
			1 => Sensor1,
			2 => Sensor2,
			3 => Sensor3,
			_ => default
		};

		internal static PlungerInputTelemetry From(in PlungerInputState state)
		{
			var telemetry = new PlungerInputTelemetry {
				SensorCount = state.SensorCount,
				ActiveSourceIndex = state.ActiveSourceIndex,
				Position = state.RawPosition,
				Velocity = state.RawVelocity,
				Sensor0 = FromSensor(state, 0),
				Sensor1 = FromSensor(state, 1),
				Sensor2 = FromSensor(state, 2),
				Sensor3 = FromSensor(state, 3)
			};
			return telemetry;
		}

		private static Sensor FromSensor(in PlungerInputState state, int index)
		{
			var sensor = state.GetSensor(index);
			return new Sensor {
				Enabled = state.IsSensorEnabled(index),
				PositionMapped = sensor.IsPositionMapped,
				VelocityMapped = sensor.IsVelocityMapped,
				Active = sensor.IsActive,
				HasPositionReading = sensor.HasPositionReading,
				PositionReading = sensor.PositionReading,
				VelocityReading = sensor.VelocityReading,
				Position = sensor.RawPosition,
				Velocity = sensor.RawVelocity,
				ReleaseApex = sensor.ReleaseApex,
				HitVelocity = sensor.GetHitVelocity(0f),
				OverflowCount = sensor.OverflowCount
			};
		}
	}
}
