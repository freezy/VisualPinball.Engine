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

#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using NLog;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.VPT.Trigger;
using Logger = NLog.Logger;


namespace VisualPinball.Unity
{
	//[PackAs("SwitchAnimation")]
	[AddComponentMenu("Pinball/Animation/Switch Animation")]
	public class SwitchAnimationComponent : AnimationComponent<TriggerData, TriggerComponent>//, IPackable
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		#region Data

		public float StartAngle {
			get => transform.localEulerAngles.x > 180 ? transform.localEulerAngles.x - 360 : transform.localEulerAngles.x;
			set => transform.SetLocalXRotation(math.radians(value));
		}

		[Range(-180f, 180f)]
		[Tooltip("Angle of the switch bracket in end position (closed)")]
		public float EndAngle;

		public AnimationCurve BackAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1f, 1);
		public float BackAnimationDurationSeconds = 0.3f;

		private float _startAngle;
		private float _currentAngle;
		private bool _ballInside;
		private int _ballId;
		private float _yEnter;
		private float _yExit;
		private Coroutine _animateBackCoroutine;

		private TriggerComponent _triggerComp;
		private PhysicsEngine _physicsEngine;

		#endregion

		private void Start()
		{
			_startAngle = StartAngle;
			_currentAngle = _startAngle;
			_triggerComp = GetComponentInParent<TriggerComponent>();
			if (!_triggerComp) {
				Logger.Warn($"{name}: No Trigger Component found in parent. Animation will not work.");
				return;
			}
			_physicsEngine = _triggerComp.GetComponentInParent<PhysicsEngine>();
			if (!_physicsEngine) {
				Logger.Warn($"{name}: No Physics Engine found in parent. Animation will not work.");
				return;
			}

			_yEnter = _triggerComp.DragPoints[0].Center.Y;
			_yExit = _triggerComp.DragPoints[1].Center.Y;

			_triggerComp.TriggerApi.Hit += OnHit;
			_triggerComp.TriggerApi.UnHit += UnHit;
		}


		private void OnHit(object sender, HitEventArgs e)
		{
			if (_ballInside) {
				// ignore other balls
				return;
			}
			if (_animateBackCoroutine != null) {
				StopCoroutine(_animateBackCoroutine);
				_animateBackCoroutine = null;
			}

			_ballInside = true;
			_ballId = e.BallId;
			Debug.Log("----------- OnHit(" + e + ")");
		}

		private void Update()
		{
			if (!_ballInside) {
				// nothing to animate
				return;
			}

			var ballTransform = _physicsEngine.GetTransform(_ballId);
			var ballLocalToWorld = (float4x4)ballTransform.localToWorldMatrix;
			var transformWithinParent = ballLocalToWorld.GetLocalToPlayfieldMatrixInVpx(_triggerComp.transform.worldToLocalMatrix);
			var localVpxPos = transformWithinParent.MultiplyPoint(ballTransform.position);

			var yPos = math.unlerp(_yEnter, _yExit, localVpxPos.y - _yEnter); // yPos is between 0 and 1, depending where localVpxPos.y is
			_currentAngle = math.clamp(math.lerp(_startAngle, EndAngle, yPos), _startAngle, EndAngle);

			transform.SetLocalXRotation(math.radians(_currentAngle));
		}

		private void UnHit(object sender, HitEventArgs e)
		{
			if (e.BallId != _ballId) {
				// ignore other balls
				return;
			}
			_ballId = 0;
			_ballInside = false;
			Debug.Log("----------- UnHit(" + e + ")");

			if (_animateBackCoroutine != null) {
				StopCoroutine(_animateBackCoroutine);
			}
			_animateBackCoroutine = StartCoroutine(AnimateBack());
		}

		private IEnumerator AnimateBack()
		{
			// rotate from _currentAngle to _startAngle
			var from = _currentAngle;
			var to = _startAngle;
			var d = to - from;

			var t = 0f;
			while (t < BackAnimationDurationSeconds) {
				var f = BackAnimationCurve.Evaluate(t / BackAnimationDurationSeconds);
				_currentAngle = from + f * d;
				transform.SetLocalXRotation(math.radians(_currentAngle));
				t += Time.deltaTime;
				yield return null;                               // wait one frame
			}

			// finally, snap to the curve's final value
			transform.SetLocalXRotation(math.radians(to));
			_animateBackCoroutine = null;
		}

		private void OnDestroy()
		{
			if (_triggerComp) {
				_triggerComp.TriggerApi.Hit -= OnHit;
				_triggerComp.TriggerApi.UnHit -= UnHit;
			}
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

#if UNITY_EDITOR

		private void OnDrawGizmosSelected()
		{

			var triggerComp = GetComponentInParent<TriggerComponent>();
			var collComp = GetComponentInParent<TriggerColliderComponent>();
			if (!triggerComp || triggerComp.DragPoints is not { Length: 4 } || !collComp) {
				return;
			}

			var dp0 = triggerComp.DragPoints[0].Center.ToUnityVector3();
			var dp1 = triggerComp.DragPoints[1].Center.ToUnityVector3();
			var dp3 = triggerComp.DragPoints[3].Center.ToUnityVector3();

			var dx = dp3.x - dp0.x;
			var h = collComp.HitHeight;

			var entryRect = new[] {
				dp0,
				new Vector3(dp0.x,      dp0.y, dp0.z + h),
				new Vector3(dp0.x + dx, dp0.y, dp0.z + h),
				new Vector3(dp0.x + dx, dp0.y, dp0.z),
				dp0
			};

			var exitRect = new[] {
				dp1,
				new Vector3(dp1.x,      dp1.y, dp1.z + h),
				new Vector3(dp1.x + dx, dp1.y, dp1.z + h),
				new Vector3(dp1.x + dx, dp1.y, dp1.z),
				dp1
			};

			Handles.matrix = triggerComp.transform.GetLocalToPlayfieldMatrixInVpx();
			Handles.color = Color.gray;

			for (var i = 0; i < 4; i++) {
				Handles2.DrawArrow(entryRect[i], exitRect[i], 3, 10);
			}
			Handles.DrawAAPolyLine(5, entryRect);
			Handles.DrawAAPolyLine(5, exitRect);

			// Gizmos.matrix = triggerComp.transform.GetLocalToPlayfieldMatrixInVpx();
			// Gizmos.color = Color.red;
			// Gizmos.DrawLineStrip(entryRect, true);
			// foreach (var v in entryRect) {
			// 	Gizmos.DrawSphere(v, 1f);
			// }
		}
#endif
	}
}
