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
using UnityEngine;

namespace VisualPinball.Unity
{
	public enum ActuatorCoilMode
	{
		FollowCoil,
		ToggleOnPulse,
		OneShot,
		FollowValue,
	}

	internal struct ActuatorMotionConfig
	{
		public ActuatorCoilMode CoilMode;
		public float ActivationDuration;
		public float ReleaseDuration;
		public AnimationCurve ActivationCurve;
		public AnimationCurve ReleaseCurve;
		public float ReleaseDelay;
		public float ActivationThreshold;
		public float OneShotHoldDuration;
	}

	/// <summary>
	/// Deterministic, Unity-frame-independent state machine behind <see cref="ActuatorComponent"/>.
	/// </summary>
	internal sealed class ActuatorMotionState
	{
		private const float PositionEpsilon = 0.000001f;

		private float _transitionFrom;
		private float _transitionElapsed;
		private float _transitionDuration;
		private AnimationCurve _transitionCurve;

		private bool _inputActive;
		private bool _releasePending;
		private float _releaseElapsed;

		private bool _oneShotCycleArmed;
		private bool _oneShotHolding;
		private float _oneShotHoldElapsed;

		internal float Position { get; private set; }
		internal float TargetPosition { get; private set; }
		internal bool IsMoving { get; private set; }
		internal bool IsInputActive => _inputActive;
		internal int ReachedSequence { get; private set; }

		internal void Initialize(float position)
		{
			Position = math.saturate(position);
			TargetPosition = Position;
			IsMoving = false;
			_inputActive = false;
			_releasePending = false;
			_releaseElapsed = 0f;
			_oneShotCycleArmed = false;
			_oneShotHolding = false;
			_oneShotHoldElapsed = 0f;
			ReachedSequence = 0;
		}

		internal void SetInput(float value, in ActuatorMotionConfig config)
		{
			value = math.saturate(value);
			if (config.CoilMode == ActuatorCoilMode.FollowValue) {
				_releasePending = false;
				_releaseElapsed = 0f;
				_inputActive = value > 0f;
				CancelOneShot();
				MoveTo(value, in config);
				return;
			}

			var active = value > math.clamp(config.ActivationThreshold, 0f, 0.999999f);
			if (active) {
				_releasePending = false;
				_releaseElapsed = 0f;
				if (_inputActive) {
					return;
				}

				_inputActive = true;
				OnRisingEdge(in config);
				return;
			}

			if (!_inputActive || _releasePending) {
				return;
			}

			var releaseDelay = math.max(0f, config.ReleaseDelay);
			if (releaseDelay <= 0f) {
				CommitRelease(in config);
			} else {
				_releasePending = true;
				_releaseElapsed = 0f;
			}
		}

		internal void Advance(float deltaTime, in ActuatorMotionConfig config)
		{
			var dt = math.max(0f, deltaTime);
			AdvancePendingRelease(dt, in config);
			var wasHoldingOneShot = _oneShotHolding;
			AdvanceMotion(dt, in config);
			if (wasHoldingOneShot) {
				AdvanceOneShotHold(dt, in config);
			}
		}

		internal void SetActive(bool active, in ActuatorMotionConfig config)
		{
			CancelOneShot();
			MoveTo(active ? 1f : 0f, in config);
		}

		internal void Toggle(in ActuatorMotionConfig config)
		{
			CancelOneShot();
			MoveTo(TargetPosition >= 0.5f ? 0f : 1f, in config);
		}

		internal void SnapTo(float position)
		{
			CancelOneShot();
			Position = math.saturate(position);
			TargetPosition = Position;
			IsMoving = false;
			_transitionElapsed = 0f;
			_transitionDuration = 0f;
		}

		private void OnRisingEdge(in ActuatorMotionConfig config)
		{
			switch (config.CoilMode) {
				case ActuatorCoilMode.FollowCoil:
					CancelOneShot();
					MoveTo(1f, in config);
					break;
				case ActuatorCoilMode.ToggleOnPulse:
					Toggle(in config);
					break;
				case ActuatorCoilMode.OneShot:
					_oneShotCycleArmed = true;
					_oneShotHolding = false;
					_oneShotHoldElapsed = 0f;
					MoveTo(1f, in config);
					if (!IsMoving && Position >= 1f - PositionEpsilon) {
						BeginOneShotHold();
					}
					break;
			}
		}

		private void AdvancePendingRelease(float deltaTime, in ActuatorMotionConfig config)
		{
			if (!_releasePending) {
				return;
			}

			_releaseElapsed += deltaTime;
			if (_releaseElapsed + PositionEpsilon >= math.max(0f, config.ReleaseDelay)) {
				CommitRelease(in config);
			}
		}

		private void CommitRelease(in ActuatorMotionConfig config)
		{
			_inputActive = false;
			_releasePending = false;
			_releaseElapsed = 0f;
			if (config.CoilMode == ActuatorCoilMode.FollowCoil) {
				MoveTo(0f, in config);
			}
		}

		private void MoveTo(float target, in ActuatorMotionConfig config)
		{
			target = math.saturate(target);
			if (math.abs(target - Position) <= PositionEpsilon) {
				var wasMoving = IsMoving;
				Position = target;
				TargetPosition = target;
				IsMoving = false;
				if (wasMoving) {
					ReachedSequence++;
				}
				return;
			}

			TargetPosition = target;
			_transitionFrom = Position;
			_transitionElapsed = 0f;
			var movingForward = target > Position;
			var fullStrokeDuration = movingForward ? config.ActivationDuration : config.ReleaseDuration;
			_transitionDuration = math.max(0f, fullStrokeDuration) * math.abs(target - Position);
			_transitionCurve = movingForward ? config.ActivationCurve : config.ReleaseCurve;

			if (_transitionDuration <= 0f) {
				Position = target;
				IsMoving = false;
				ReachedSequence++;
				return;
			}

			IsMoving = true;
		}

		private void AdvanceMotion(float deltaTime, in ActuatorMotionConfig config)
		{
			if (!IsMoving) {
				return;
			}

			_transitionElapsed += deltaTime;
			if (_transitionElapsed + PositionEpsilon >= _transitionDuration) {
				Position = TargetPosition;
				IsMoving = false;
				ReachedSequence++;
				if (_oneShotCycleArmed && Position >= 1f - PositionEpsilon) {
					BeginOneShotHold();
				}
				return;
			}

			var t = _transitionElapsed / _transitionDuration;
			var curveValue = EvaluateCurve(_transitionCurve, t);
			Position = math.saturate(math.lerp(_transitionFrom, TargetPosition, curveValue));
		}

		private void BeginOneShotHold()
		{
			_oneShotHolding = true;
			_oneShotHoldElapsed = 0f;
		}

		private void AdvanceOneShotHold(float deltaTime, in ActuatorMotionConfig config)
		{
			if (!_oneShotHolding) {
				return;
			}

			if (config.CoilMode != ActuatorCoilMode.OneShot) {
				CancelOneShot();
				return;
			}

			_oneShotHoldElapsed += deltaTime;
			if (_oneShotHoldElapsed + PositionEpsilon < math.max(0f, config.OneShotHoldDuration)) {
				return;
			}

			_oneShotHolding = false;
			_oneShotCycleArmed = false;
			_oneShotHoldElapsed = 0f;
			MoveTo(0f, in config);
		}

		private void CancelOneShot()
		{
			_oneShotCycleArmed = false;
			_oneShotHolding = false;
			_oneShotHoldElapsed = 0f;
		}

		internal static float EvaluateCurve(AnimationCurve curve, float time)
		{
			var t = math.saturate(time);
			return curve == null || curve.length < 2 ? t : curve.Evaluate(t);
		}
	}
}
