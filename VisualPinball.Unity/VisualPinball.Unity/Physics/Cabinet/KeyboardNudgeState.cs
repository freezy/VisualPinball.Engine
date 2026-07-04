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
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	public struct KeyboardNudgeState
	{
		private const int DeactivationDelayMs = 10000;
		private const int CabImpulseLengthMs = 25;

		public KeyboardNudgeMode Mode;
		public float Strength;
		public float CabinetDamping;
		public float2 CabinetAcceleration;
		public float2 CabinetOffset;

		private float2 _pushImpulse;
		private int _pushNudgeTime;
		private int _pushDeactivationDelay;

		private float2 _boxVelocity;
		private float2 _boxPrevVelocity;
		private float2 _boxPositionVpu;
		private float _boxSpring;
		private float _boxDamping;
		private int _boxDeactivationDelay;

		private CabinetPhysicsState _cabinet;
		private FixedList512Bytes<CabinetImpulse> _cabImpulses;
		private int _cabDeactivationDelay;

		private struct CabinetImpulse
		{
			public int Length;
			public int Elapsed;
			public float2 Acceleration;

			public CabinetImpulse(int length, float2 acceleration)
			{
				Length = length;
				Elapsed = 0;
				Acceleration = acceleration;
			}

			public bool IsInProgress => Elapsed <= Length;

			public float2 CurrentAcceleration
			{
				get {
					if (!IsInProgress) {
						return float2.zero;
					}
					var t = (float)Elapsed / Length;
					return Acceleration * 0.5f * (1f - math.cos(2f * math.PI * t));
				}
			}
		}

		public KeyboardNudgeState(KeyboardNudgeMode mode, float strength, float nudgeTime,
			float cabinetDamping = CabinetPhysicsState.DefaultKeyboardDampingRatio)
		{
			Mode = mode;
			Strength = strength;
			CabinetDamping = math.clamp(cabinetDamping,
				CabinetPhysicsState.MinKeyboardDampingRatio,
				CabinetPhysicsState.MaxKeyboardDampingRatio);
			CabinetAcceleration = float2.zero;
			CabinetOffset = float2.zero;
			_pushImpulse = float2.zero;
			_pushNudgeTime = 0;
			_pushDeactivationDelay = 0;
			_boxVelocity = float2.zero;
			_boxPrevVelocity = float2.zero;
			_boxPositionVpu = float2.zero;
			_boxSpring = 0f;
			_boxDamping = 0f;
			_boxDeactivationDelay = 0;
			_cabinet = CabinetPhysicsState.Keyboard(CabinetDamping);
			_cabImpulses = default;
			_cabDeactivationDelay = 0;

			ConfigureBoxModel(nudgeTime);
		}

		public bool IsActive => Mode switch {
			KeyboardNudgeMode.PushRetract => _pushDeactivationDelay > 0,
			KeyboardNudgeMode.BoxModel => _boxDeactivationDelay > 0,
			_ => _cabDeactivationDelay > 0,
		};

		public void Configure(KeyboardNudgeMode mode, float strength, float nudgeTime, float cabinetDamping)
		{
			this = new KeyboardNudgeState(mode, strength, nudgeTime, cabinetDamping);
		}

		public void SetStrength(float strength)
		{
			Strength = strength;
		}

		public void Nudge(float angleDeg, float force)
		{
			switch (Mode) {
				case KeyboardNudgeMode.PushRetract:
					PushRetractNudge(angleDeg, force);
					break;
				case KeyboardNudgeMode.BoxModel:
					BoxModelNudge(angleDeg, force);
					break;
				default:
					CabModelNudge(angleDeg, force);
					break;
			}
		}

		public void StepOneMillisecond()
		{
			switch (Mode) {
				case KeyboardNudgeMode.PushRetract:
					StepPushRetract();
					break;
				case KeyboardNudgeMode.BoxModel:
					StepBoxModel();
					break;
				default:
					StepCabModel();
					break;
			}
		}

		private void ConfigureBoxModel(float nudgeTime)
		{
			nudgeTime = math.max(0.001f, nudgeTime);
			const float dampingRatio = 0.5f;
			_boxSpring = math.PI * math.PI / (nudgeTime * nudgeTime * (1f - dampingRatio * dampingRatio));
			_boxDamping = dampingRatio * 2f * math.sqrt(_boxSpring);
		}

		private void PushRetractNudge(float angleDeg, float force)
		{
			_pushDeactivationDelay = DeactivationDelayMs;
			if (_pushNudgeTime != 0) {
				return;
			}

			var angle = math.radians(angleDeg);
			_pushImpulse = new float2(math.sin(angle), -math.cos(angle)) * (Strength * force);
			_pushNudgeTime = 100;
		}

		private void StepPushRetract()
		{
			if (_pushDeactivationDelay > 0) {
				_pushDeactivationDelay--;
			}

			if (_pushNudgeTime != 0) {
				_pushNudgeTime--;
				if (_pushNudgeTime == 95) {
					CabinetAcceleration = new float2(-_pushImpulse.x * 2f, _pushImpulse.y * 2f)
						* (1f / PhysicsConstants.PhysFactor)
						* (1f / PhysicsConstants.Ms2ToVpuVpt2);
				} else if (_pushNudgeTime == 90) {
					CabinetAcceleration = new float2(_pushImpulse.x, -_pushImpulse.y)
						* (1f / PhysicsConstants.PhysFactor)
						* (1f / PhysicsConstants.Ms2ToVpuVpt2);
				} else {
					CabinetAcceleration = float2.zero;
				}
			} else {
				CabinetAcceleration = float2.zero;
			}

			var attenuation = math.pow(_pushNudgeTime * 0.01f, 2f);
			CabinetOffset = new float2(_pushImpulse.x, -_pushImpulse.y) * attenuation * PhysicsConstants.VpuToM;
		}

		private void BoxModelNudge(float angleDeg, float force)
		{
			_boxDeactivationDelay = DeactivationDelayMs;
			var angle = math.radians(angleDeg);
			_boxVelocity += new float2(math.sin(angle), -math.cos(angle)) * (Strength * force);
		}

		private void StepBoxModel()
		{
			if (_boxDeactivationDelay > 0) {
				_boxDeactivationDelay--;
			}

			var force = -_boxSpring * _boxPositionVpu - _boxDamping * _boxVelocity;
			_boxVelocity += PhysicsConstants.PhysFactor * force;
			_boxPositionVpu += PhysicsConstants.PhysFactor * _boxVelocity;
			CabinetOffset = _boxPositionVpu * PhysicsConstants.VpuToM;
			CabinetAcceleration = (_boxVelocity - _boxPrevVelocity)
				* (1f / PhysicsConstants.PhysFactor)
				* (1f / PhysicsConstants.Ms2ToVpuVpt2);
			_boxPrevVelocity = _boxVelocity;
		}

		private void CabModelNudge(float angleDeg, float force)
		{
			_cabDeactivationDelay = DeactivationDelayMs;
			var angle = math.radians(angleDeg);
			var acceleration = new float2(math.sin(angle), -math.cos(angle)) * (force * Strength * 6f);
			if (_cabImpulses.Length < _cabImpulses.Capacity) {
				_cabImpulses.Add(new CabinetImpulse(CabImpulseLengthMs, acceleration));
			}
		}

		private void StepCabModel()
		{
			if (_cabDeactivationDelay > 0) {
				_cabDeactivationDelay--;
			}

			var impulse = float2.zero;
			for (var i = 0; i < _cabImpulses.Length;) {
				var cabImpulse = _cabImpulses[i];
				cabImpulse.Elapsed++;
				if (cabImpulse.IsInProgress) {
					impulse += cabImpulse.CurrentAcceleration;
					_cabImpulses[i] = cabImpulse;
					i++;
				} else {
					RemoveCabImpulseAt(i);
				}
			}

			_cabinet.StepOneMillisecond(_cabinet.Mass * impulse);
			CabinetAcceleration = _cabinet.CabinetAcceleration;
			CabinetOffset = _cabinet.CabinetOffset;
		}

		private void RemoveCabImpulseAt(int index)
		{
			for (var i = index + 1; i < _cabImpulses.Length; i++) {
				_cabImpulses[i - 1] = _cabImpulses[i];
			}
			_cabImpulses.Length--;
		}
	}
}
