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

using Unity.Collections;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public struct PlumbState
	{
		private const float Gravity = 9.80665f;
		private const float DefaultPoleLength = 0.10f;
		private const float DampingCoef0 = 1.25f;
		private const float DampingCoef1 = 0.75f;

		public float PoleLength;
		public float TiltThresholdRad;
		public float3 Position;
		public float3 AngularVelocity;
		public int TiltIndex;
		public float MaxTiltPercent;
		public float2 MaxPlumbPosition;
		public FixedList32Bytes<byte> PendingTiltStates;

		private byte _enabled;
		private byte _tiltHigh;
		private float _angularDamping0;
		private float _angularDamping1;
		private float _cabinetAccelerationScale;

		public bool Enabled
		{
			get => _enabled != 0;
			set => _enabled = value ? (byte)1 : (byte)0;
		}

		public bool TiltHigh
		{
			get => _tiltHigh != 0;
			private set => _tiltHigh = value ? (byte)1 : (byte)0;
		}

		public PlumbState(bool enabled, float damping, float tiltThresholdDeg)
		{
			PoleLength = DefaultPoleLength;
			TiltThresholdRad = math.radians(math.clamp(tiltThresholdDeg, 0.5f, 4f));
			Position = new float3(0f, 0f, -DefaultPoleLength);
			AngularVelocity = float3.zero;
			TiltIndex = 0;
			MaxTiltPercent = 0f;
			MaxPlumbPosition = float2.zero;
			PendingTiltStates = default;
			_enabled = enabled ? (byte)1 : (byte)0;
			_tiltHigh = 0;
			_angularDamping0 = DampingCoef0 * math.clamp(damping, 0f, 2f);
			_angularDamping1 = DampingCoef1 * math.clamp(damping, 0f, 2f);
			_cabinetAccelerationScale = 1f;
		}

		public void Configure(bool enabled, float damping, float tiltThresholdDeg)
		{
			this = new PlumbState(enabled, damping, tiltThresholdDeg);
		}

		public void StepOneMillisecond(float2 cabinetAcceleration)
		{
			if (!Enabled || TiltThresholdRad <= 0f) {
				return;
			}

			const float dt = 0.001f;
			var poleAxis = Position / PoleLength;
			var plumbAcceleration = new float3(
				-cabinetAcceleration.x * _cabinetAccelerationScale,
				-cabinetAcceleration.y * _cabinetAccelerationScale,
				-Gravity
			);

			var torque = math.cross(Position, plumbAcceleration);
			var alpha = torque / (PoleLength * PoleLength);
			var damping = _angularDamping0 + _angularDamping1 * math.length(AngularVelocity);
			alpha -= AngularVelocity * damping;

			AngularVelocity += alpha * dt;
			AngularVelocity -= poleAxis * math.dot(AngularVelocity, poleAxis);

			Position += math.cross(AngularVelocity, Position) * dt;
			var positionLength = math.length(Position);
			if (positionLength > 1.0e-8f) {
				Position *= PoleLength / positionLength;
			} else {
				Position = new float3(0f, 0f, -PoleLength);
			}

			poleAxis = Position / PoleLength;
			AngularVelocity -= poleAxis * math.dot(AngularVelocity, poleAxis);

			var psi = math.atan2(math.sqrt(Position.x * Position.x + Position.y * Position.y), -Position.z);
			var tiltPercent = 100f * psi / TiltThresholdRad;
			var tilted = false;
			if (tiltPercent > 100f) {
				tilted = true;
				ClampToTiltRingAndBounce(TiltThresholdRad);
			}

			if (TiltHigh != tilted) {
				TiltHigh = tilted;
				if (tilted) {
					TiltIndex++;
				}
				if (PendingTiltStates.Length < PendingTiltStates.Capacity) {
					PendingTiltStates.Add(tilted ? (byte)1 : (byte)0);
				}
			}

			if (tiltPercent > MaxTiltPercent) {
				MaxTiltPercent = tiltPercent;
			}
			if (math.abs(Position.x) > math.abs(MaxPlumbPosition.x)) {
				MaxPlumbPosition.x = Position.x;
			}
			if (math.abs(Position.y) > math.abs(MaxPlumbPosition.y)) {
				MaxPlumbPosition.y = Position.y;
			}
		}

		public float3 ReadAndResetTiltStatus()
		{
			var value = new float3(MaxPlumbPosition.x, MaxPlumbPosition.y, MaxTiltPercent);
			MaxPlumbPosition = float2.zero;
			MaxTiltPercent = 0f;
			return value;
		}

		public void ClearPendingTiltEvents()
		{
			PendingTiltStates.Length = 0;
		}

		private void ClampToTiltRingAndBounce(float tiltAngle)
		{
			var limitAngle = tiltAngle - 1e-3f;
			Position.z = -PoleLength * math.cos(limitAngle);
			var xy = PoleLength * math.sin(limitAngle);
			var theta = math.atan2(Position.x, Position.y);
			var axis = new float3(math.sin(theta), math.cos(theta), 0f);
			Position.x = xy * axis.x;
			Position.y = xy * axis.y;

			var poleAxis = Position / PoleLength;
			var velocity = math.cross(AngularVelocity, Position);
			var reflectedVelocity = velocity - 2f * math.dot(velocity, poleAxis) * poleAxis;
			AngularVelocity = math.cross(Position, reflectedVelocity) / (PoleLength * PoleLength);
			AngularVelocity *= 0.8f;
		}
	}
}
