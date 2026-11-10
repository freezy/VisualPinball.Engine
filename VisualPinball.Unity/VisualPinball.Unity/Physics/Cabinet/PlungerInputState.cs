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

using Unity.Mathematics;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Owns the analog plunger sensors of the cabinet and exposes the single
	/// plunger reading the physics step uses.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Port of <c>PlungerHandler</c> in <c>vpinball/src/input/PlungerHandler.cpp</c>.
	/// The first active sensor wins; an external override (a plugin or remote
	/// control) wins over all sensors.
	/// </para>
	/// <para>
	/// Units: p.u. are relative to the physical plunger, 0 at rest and +1 fully
	/// retracted; t.u. are relative to the table's plunger, 0 fully extended,
	/// 1 fully retracted and the rest position in between.
	/// </para>
	/// </remarks>
	public struct PlungerInputState
	{
		public const int MaxSensors = 4;

		/// <summary><see cref="ActiveSourceIndex"/> while no source is active.</summary>
		public const int NoSource = -1;

		/// <summary><see cref="ActiveSourceIndex"/> while the external override is active.</summary>
		public const int ExternalSource = -2;

		public PlungerSensorState Sensor0;
		public PlungerSensorState Sensor1;
		public PlungerSensorState Sensor2;
		public PlungerSensorState Sensor3;
		public int SensorCount;

		// 0: no source, -1: external override, n: sensor n - 1. Zero-initialized means "no source".
		private int _activeSlot;

		private byte _sensorEnabled0;
		private byte _sensorEnabled1;
		private byte _sensorEnabled2;
		private byte _sensorEnabled3;

		private byte _externalOverride;
		private float _externalPosition;
		private float _externalVelocity;

		private float _position;
		private float _rawVelocity;

		/// <summary>Position of the active source in p.u.</summary>
		public float RawPosition => _position;

		/// <summary>Velocity of the active source in p.u./s.</summary>
		public float RawVelocity => _rawVelocity;

		/// <summary>Sensor index the current reading comes from, or <see cref="NoSource"/> / <see cref="ExternalSource"/>.</summary>
		public int ActiveSourceIndex => _activeSlot switch {
			0 => NoSource,
			-1 => ExternalSource,
			_ => _activeSlot - 1
		};

		/// <summary>True if any source currently drives the plunger.</summary>
		public bool HasSensor => _activeSlot != 0;

		public bool IsExternalOverride => _externalOverride != 0;

		/// <summary>
		/// Sets the number of sensor slots and clears the slots beyond it.
		/// </summary>
		public void ConfigureSensors(int count)
		{
			SensorCount = math.clamp(count, 0, MaxSensors);
			for (var i = SensorCount; i < MaxSensors; i++) {
				ConfigureSensor(i, false, false, false);
			}
			UpdateActiveSource();
		}

		/// <summary>
		/// Rebuilds one sensor slot. A disabled slot keeps its mapping but never becomes active.
		/// </summary>
		public void ConfigureSensor(int index, bool positionMapped, bool velocityMapped, bool enabled)
		{
			if ((uint)index >= MaxSensors) {
				return;
			}
			var sensor = new PlungerSensorState(positionMapped, velocityMapped);
			var enabledByte = enabled ? (byte)1 : (byte)0;
			switch (index) {
				case 0: Sensor0 = sensor; _sensorEnabled0 = enabledByte; break;
				case 1: Sensor1 = sensor; _sensorEnabled1 = enabledByte; break;
				case 2: Sensor2 = sensor; _sensorEnabled2 = enabledByte; break;
				case 3: Sensor3 = sensor; _sensorEnabled3 = enabledByte; break;
			}
			if (enabled && index >= SensorCount) {
				SensorCount = index + 1;
			}
			UpdateActiveSource();
		}

		/// <summary>
		/// Enables or disables a slot without changing its mapping. Disabling deactivates it.
		/// </summary>
		public void SetSensorEnabled(int index, bool enabled)
		{
			if ((uint)index >= MaxSensors) {
				return;
			}
			var enabledByte = enabled ? (byte)1 : (byte)0;
			switch (index) {
				case 0: _sensorEnabled0 = enabledByte; Sensor0.Deactivate(); break;
				case 1: _sensorEnabled1 = enabledByte; Sensor1.Deactivate(); break;
				case 2: _sensorEnabled2 = enabledByte; Sensor2.Deactivate(); break;
				case 3: _sensorEnabled3 = enabledByte; Sensor3.Deactivate(); break;
			}
			UpdateActiveSource();
		}

		public bool IsSensorEnabled(int index)
		{
			return index switch {
				0 => _sensorEnabled0 != 0,
				1 => _sensorEnabled1 != 0,
				2 => _sensorEnabled2 != 0,
				3 => _sensorEnabled3 != 0,
				_ => false
			};
		}

		/// <summary>
		/// Returns a copy of one sensor slot, for telemetry.
		/// </summary>
		public PlungerSensorState GetSensor(int index)
		{
			return index switch {
				0 => Sensor0,
				1 => Sensor1,
				2 => Sensor2,
				3 => Sensor3,
				_ => default
			};
		}

		/// <summary>
		/// Overrides all sensors with an externally provided state, in p.u. and p.u./s.
		/// </summary>
		public void SetExternalPlunger(bool enableOverride, float velocity, float position)
		{
			_externalOverride = enableOverride ? (byte)1 : (byte)0;
			_externalVelocity = math.isfinite(velocity) ? velocity : 0f;
			_externalPosition = math.isfinite(position) ? position : 0f;
			UpdateActiveSource();
		}

		/// <summary>
		/// Stages one mapped reading of a sensor for the batch that ends at <paramref name="batchEndUsec"/>.
		/// </summary>
		/// <returns>False if the sensor's staging buffer overflowed.</returns>
		public bool Stage(int sensorIndex, PlungerSensorChannel channel, float value, long hostTimeUsec, ulong batchEndUsec)
		{
			if ((uint)sensorIndex >= SensorCount || !IsSensorEnabled(sensorIndex)) {
				return true;
			}
			return sensorIndex switch {
				0 => Sensor0.Stage(channel, value, hostTimeUsec, batchEndUsec),
				1 => Sensor1.Stage(channel, value, hostTimeUsec, batchEndUsec),
				2 => Sensor2.Stage(channel, value, hostTimeUsec, batchEndUsec),
				3 => Sensor3.Stage(channel, value, hostTimeUsec, batchEndUsec),
				_ => true
			};
		}

		/// <summary>
		/// Corrects every sensor's clock offset after a batch was staged.
		/// </summary>
		public void EndStaging(ulong batchEndUsec)
		{
			Sensor0.EndStaging(batchEndUsec);
			Sensor1.EndStaging(batchEndUsec);
			Sensor2.EndStaging(batchEndUsec);
			Sensor3.EndStaging(batchEndUsec);
		}

		/// <summary>
		/// Drops staged samples and filter state of all sensors, keeping activation.
		/// </summary>
		/// <remarks>Call it whenever simulation time stops following host time.</remarks>
		public void ResetClocks()
		{
			Sensor0.ResetClock();
			Sensor1.ResetClock();
			Sensor2.ResetClock();
			Sensor3.ResetClock();
		}

		/// <summary>
		/// Deactivates one sensor and drops all its state, e.g. after its device disappeared.
		/// </summary>
		public void DeactivateSensor(int index)
		{
			switch (index) {
				case 0: Sensor0.Deactivate(); break;
				case 1: Sensor1.Deactivate(); break;
				case 2: Sensor2.Deactivate(); break;
				case 3: Sensor3.Deactivate(); break;
			}
			UpdateActiveSource();
		}

		/// <summary>
		/// Advances all sensors to <paramref name="nowUsec"/>, the simulation
		/// time at the end of this millisecond, and selects the active source.
		/// </summary>
		public void StepOneMillisecond(ulong nowUsec)
		{
			if (SensorCount > 0 && _sensorEnabled0 != 0) Sensor0.StepOneMillisecond(nowUsec);
			if (SensorCount > 1 && _sensorEnabled1 != 0) Sensor1.StepOneMillisecond(nowUsec);
			if (SensorCount > 2 && _sensorEnabled2 != 0) Sensor2.StepOneMillisecond(nowUsec);
			if (SensorCount > 3 && _sensorEnabled3 != 0) Sensor3.StepOneMillisecond(nowUsec);
			UpdateActiveSource();
		}

		/// <summary>
		/// Plunger position mapped to the table plunger, in t.u.
		/// </summary>
		/// <remarks>
		/// 0 maps to the rest position, +1 to fully retracted, and the same scale
		/// continues forward of rest, clamped at fully extended.
		/// </remarks>
		public float GetPosition(float restPos)
		{
			return math.saturate(math.lerp(restPos, 1f, _position));
		}

		/// <summary>
		/// Velocity at which the plunger hits a ball, in t.u./s, 0 if none.
		/// </summary>
		public float GetHitVelocity(float restPos)
		{
			if (_externalOverride != 0) {
				return _externalVelocity * (1f - restPos);
			}
			return ActiveSourceIndex switch {
				0 => Sensor0.GetHitVelocity(restPos),
				1 => Sensor1.GetHitVelocity(restPos),
				2 => Sensor2.GetHitVelocity(restPos),
				3 => Sensor3.GetHitVelocity(restPos),
				_ => 0f
			};
		}

		private void UpdateActiveSource()
		{
			if (_externalOverride != 0) {
				_activeSlot = -1;
				_position = _externalPosition;
				_rawVelocity = _externalVelocity;
				return;
			}
			_activeSlot = 0;
			_position = 0f;
			_rawVelocity = 0f;
			if (TrySelect(0, in Sensor0, _sensorEnabled0)) return;
			if (TrySelect(1, in Sensor1, _sensorEnabled1)) return;
			if (TrySelect(2, in Sensor2, _sensorEnabled2)) return;
			TrySelect(3, in Sensor3, _sensorEnabled3);
		}

		private bool TrySelect(int index, in PlungerSensorState sensor, byte enabled)
		{
			if (index >= SensorCount || enabled == 0 || !sensor.IsActive) {
				return false;
			}
			_activeSlot = index + 1;
			_position = sensor.RawPosition;
			_rawVelocity = sensor.RawVelocity;
			return true;
		}
	}
}
