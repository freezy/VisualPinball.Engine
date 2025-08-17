// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

// ReSharper disable InconsistentNaming

using System.Collections;
using NLog;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	//[PackAs("DropTargetAnimation")]
	[AddComponentMenu("Pinball/Animation/Drop Target Animation")]
	public class DropTargetAnimationComponent : AnimationComponent<bool>//, TODO IPackable
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		#region Data

		[Range(-180f, 180f)]
		[Tooltip("How far the drop target rotates back, when hit.")]
		public float RotationAngle = 2f;

		[Range(-180f, 180f)]
		[Tooltip("Duration of the rotation, in seconds.")]
		public float RotationDuration = 0.1f;

		[Tooltip("Animation curve of the rotation. Must rotate back and forth.")]
		public AnimationCurve RotationAnimationCurve = new(
			new Keyframe(0f, 0),
			new Keyframe(0.5f, 1),
			new Keyframe(1f, 0)
		);

		[Tooltip("The length the target drops, in VPX units.")]
		public float DropDistance = 52.0f;

		[Tooltip("Time in seconds after the hit for the drop target to drop.")]
		public float DropDelay = 0.1f;

		[Tooltip("Duration of the drop, in seconds.")]
		public float DropDuration = 0.3f;

		[Tooltip("Animation curve of the drop animation.")]
		public AnimationCurve DropAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1f, 1);

		[Tooltip("Duration of the pull-up, in seconds.")]
		public float PullUpDuration = 0.5f;

		[Tooltip("Animation curve of the drop animation.")]
		public AnimationCurve PullUpAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1f, 1);

		private float _startPos;
		private bool _isAnimating;

		private Keyboard _keyboard;
		private IGamelogicEngine _gle;

		#endregion

		private void Start()
		{
			_startPos = transform.localPosition.y;
		}

		protected override void OnAnimationValueChanged(bool value)
		{
			if (value) {
				OnHit();
			} else {
				OnReset();
			}
		}

		private void OnHit()
		{
			if (_isAnimating) {
				return;
			}

			_isAnimating = true;
			StartCoroutine(AnimateRotation());
			if (DropDelay == 0f) {
				StartCoroutine(AnimateDrop());
			}
		}

		private void OnReset()
		{
			if (_isAnimating) {
				return;
			}

			_isAnimating = true;
			StartCoroutine(AnimateReset());
		}

		private IEnumerator AnimateRotation()
		{
			var t = 0f;
			while (t < RotationDuration) {
				var f = RotationAnimationCurve.Evaluate(t / RotationDuration);
				transform.SetLocalXRotation(math.radians(f * RotationAngle));
				t += Time.deltaTime;
				if (DropDelay != 0 && t >= DropDelay) {
					StartCoroutine(AnimateDrop());
				}
				yield return null;                               // wait one frame
			}

			// snap back to the start
			transform.SetLocalXRotation(0);
		}

		private IEnumerator AnimateDrop()
		{
			var t = 0f;
			while (t < DropDuration) {
				var f = DropAnimationCurve.Evaluate(t / DropDuration);
				var pos = transform.localPosition;
				pos.y = _startPos - f * Physics.ScaleToWorld(DropDistance);
				transform.localPosition = pos;
				t += Time.deltaTime;
				yield return null;                               // wait one frame
			}

			// finally, snap to the curve's final value
			var finalPos = transform.localPosition;
			finalPos.y = _startPos - Physics.ScaleToWorld(DropDistance);
			transform.localPosition = finalPos;
			_isAnimating = false;
		}

		private IEnumerator AnimateReset()
		{
			var t = 0f;
			while (t < PullUpDuration) {
				var f = PullUpAnimationCurve.Evaluate(t / PullUpDuration);
				var pos = transform.localPosition;
				pos.y = _startPos - Physics.ScaleToWorld(DropDistance) + f * Physics.ScaleToWorld(DropDistance);
				transform.localPosition = pos;
				t += Time.deltaTime;
				yield return null;                               // wait one frame
			}

			// finally, snap to the curve's final value
			var finalPos = transform.localPosition;
			finalPos.y = _startPos;
			transform.localPosition = finalPos;
			_isAnimating = false;
		}

		// #region Packaging
		//
		// public byte[] Pack() => TriggerAnimationPackable.Pack(this);
		//
		// public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => null;
		//
		// public void Unpack(byte[] bytes) => TriggerAnimationPackable.Unpack(bytes, this);
		//
		// public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) { }
		//
		// #endregion

	}
}
