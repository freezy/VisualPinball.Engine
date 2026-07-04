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
	public struct NudgeIntentState
	{
		private const int ImpulseLengthMs = 25;
		private const float ImpulseThreshold = 1.0f;

		private byte _isGamepad;
		private int _impulseElapsed;
		private float2 _impulse;
		private ulong _time;
		private float _segmentStrength;
		private ulong _segmentStart;
		private ulong _segmentEnd;
		private byte _segmentIsPeak;
		private byte _segmentImpulseSent;
		private float _lastImpulseStrength;
		private ulong _lastImpulseTime;

		public NudgeIntentState(bool isGamepad)
		{
			_isGamepad = isGamepad ? (byte)1 : (byte)0;
			_impulseElapsed = ImpulseLengthMs + 1;
			_impulse = float2.zero;
			_time = 0;
			_segmentStrength = 0f;
			_segmentStart = 0;
			_segmentEnd = 0;
			_segmentIsPeak = 0;
			_segmentImpulseSent = 0;
			_lastImpulseStrength = 0f;
			_lastImpulseTime = 0;
		}

		public bool IsImpulseInProgress => _impulseElapsed <= ImpulseLengthMs;
		public float2 ImpulseAcceleration
		{
			get {
				if (!IsImpulseInProgress) {
					return float2.zero;
				}
				var t = (float)_impulseElapsed / ImpulseLengthMs;
				return _impulse * 0.5f * (1f - math.cos(2f * math.PI * t));
			}
		}

		public void StepOneMillisecond(float2 nudgeAcceleration)
		{
			_impulseElapsed++;
			_time++;

			var nudge = new float2(nudgeAcceleration.x, math.min(nudgeAcceleration.y, 0f));
			var strength = math.length(nudge);

			if (_segmentIsPeak != 0) {
				if (strength > _segmentStrength) {
					_segmentStrength = strength;
					_segmentEnd = _time;
					if (_segmentImpulseSent == 0) {
						EvaluateImpulse(nudge);
					} else {
						var newImpulse = GetImpulseStrengthFactor() * nudge;
						if (math.lengthsq(newImpulse) > math.lengthsq(_impulse)) {
							_impulse = newImpulse;
						}
					}
				} else if (strength < _segmentStrength * 0.9f) {
					_lastImpulseTime = _segmentEnd;
					_lastImpulseStrength = _segmentStrength;
					_segmentStrength = strength;
					_segmentStart = _time;
					_segmentEnd = _time;
					_segmentIsPeak = 0;
				}
			} else {
				if (strength < _segmentStrength) {
					_segmentStrength = strength;
					_segmentEnd = _time;
				} else if (strength > math.max(0.1f, _segmentStrength * 1.1f)) {
					_segmentStrength = strength;
					_segmentStart = _time;
					_segmentEnd = _time;
					_segmentIsPeak = 1;
					_segmentImpulseSent = 0;
					EvaluateImpulse(nudge);
				}
			}
		}

		private float GetImpulseStrengthFactor()
		{
			return 1f;
		}

		private void EvaluateImpulse(float2 impulse)
		{
			var strengthFactor = GetImpulseStrengthFactor();
			var fireImpulse = strengthFactor * _segmentStrength > ImpulseThreshold;
			if (_isGamepad == 0) {
				fireImpulse &= _segmentStrength > _lastImpulseStrength || _segmentEnd - _lastImpulseTime > 300;
			}

			if (!fireImpulse) {
				return;
			}

			_impulse = strengthFactor * impulse;
			_impulseElapsed = 0;
			_segmentImpulseSent = 1;
		}
	}
}
