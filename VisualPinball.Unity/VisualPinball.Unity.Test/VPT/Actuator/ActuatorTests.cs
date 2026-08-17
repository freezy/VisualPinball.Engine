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

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test.VPT.Actuator
{
	public class ActuatorTests
	{
		[Test]
		public void RepeatedNonzeroSamplesToggleOnlyOnce()
		{
			var state = CreateState();
			var config = Config(ActuatorCoilMode.ToggleOnPulse, activationDuration: 0f, releaseDuration: 0f);

			state.SetInput(64f / 255f, in config);
			state.SetInput(1f, in config);
			state.SetInput(0.5f, in config);

			Assert.That(state.Position, Is.EqualTo(1f));
			Assert.That(state.TargetPosition, Is.EqualTo(1f));
		}

		[Test]
		public void ShortInactiveGapDoesNotRearmToggle()
		{
			var state = CreateState();
			var config = Config(ActuatorCoilMode.ToggleOnPulse, activationDuration: 0f, releaseDuration: 0f, releaseDelay: 0.05f);

			state.SetInput(1f, in config);
			state.SetInput(0f, in config);
			state.Advance(0.02f, in config);
			state.SetInput(1f, in config);

			Assert.That(state.Position, Is.EqualTo(1f));
			Assert.That(state.IsInputActive, Is.True);
		}

		[Test]
		public void SustainedInactiveGapRearmsToggle()
		{
			var state = CreateState();
			var config = Config(ActuatorCoilMode.ToggleOnPulse, activationDuration: 0f, releaseDuration: 0f, releaseDelay: 0.05f);

			state.SetInput(1f, in config);
			state.SetInput(0f, in config);
			state.Advance(0.05f, in config);
			state.SetInput(1f, in config);

			Assert.That(state.Position, Is.EqualTo(0f));
			Assert.That(state.TargetPosition, Is.EqualTo(0f));
		}

		[Test]
		public void GodzillaBridgeLevelIsOneBinaryActivationNotPosition()
		{
			var state = CreateState();
			var config = Config(ActuatorCoilMode.ToggleOnPulse, activationDuration: 0f, releaseDuration: 0f);

			state.SetInput(64f / 255f, in config);

			Assert.That(state.Position, Is.EqualTo(1f));
			Assert.That(state.Position, Is.Not.EqualTo(64f / 255f));
		}

		[Test]
		public void ValuesAtOrBelowActivationThresholdDoNotActivate()
		{
			var state = CreateState();
			var config = Config(ActuatorCoilMode.ToggleOnPulse, activationDuration: 0f, releaseDuration: 0f, activationThreshold: 0.2f);

			state.SetInput(0.19f, in config);
			state.SetInput(0.2f, in config);

			Assert.That(state.Position, Is.EqualTo(0f));
			Assert.That(state.IsInputActive, Is.False);

			state.SetInput(0.21f, in config);

			Assert.That(state.Position, Is.EqualTo(1f));
			Assert.That(state.IsInputActive, Is.True);
		}

		[Test]
		public void PendingReleaseCanBeCancelled()
		{
			var state = CreateState();
			var config = Config(ActuatorCoilMode.FollowCoil, activationDuration: 0f, releaseDuration: 0f, releaseDelay: 0.05f);

			state.SetInput(1f, in config);
			state.SetInput(0f, in config);
			state.Advance(0.04f, in config);
			state.SetInput(1f, in config);
			state.Advance(0.02f, in config);

			Assert.That(state.Position, Is.EqualTo(1f));
			Assert.That(state.IsInputActive, Is.True);
		}

		[Test]
		public void FollowCoilWaitsForReleaseDelay()
		{
			var state = CreateState();
			var config = Config(ActuatorCoilMode.FollowCoil, activationDuration: 0f, releaseDuration: 0f, releaseDelay: 0.05f);

			state.SetInput(1f, in config);
			state.SetInput(0f, in config);
			state.Advance(0.04f, in config);
			Assert.That(state.Position, Is.EqualTo(1f));

			state.Advance(0.01f, in config);
			Assert.That(state.Position, Is.EqualTo(0f));
		}

		[Test]
		public void OneShotTravelsHoldsAndReturns()
		{
			var state = CreateState();
			var config = Config(ActuatorCoilMode.OneShot, activationDuration: 0.1f, releaseDuration: 0.1f, oneShotHoldDuration: 0.2f);

			state.SetInput(1f, in config);
			state.Advance(0.1f, in config);
			Assert.That(state.Position, Is.EqualTo(1f));

			state.Advance(0.19f, in config);
			Assert.That(state.TargetPosition, Is.EqualTo(1f));
			state.Advance(0.01f, in config);
			Assert.That(state.TargetPosition, Is.EqualTo(0f));

			state.Advance(0.1f, in config);
			Assert.That(state.Position, Is.EqualTo(0f));
		}

		[Test]
		public void OneShotHeldCoilMustReleaseBeforeRetriggering()
		{
			var state = CreateState();
			var config = Config(ActuatorCoilMode.OneShot, activationDuration: 0f, releaseDuration: 0f, releaseDelay: 0f, oneShotHoldDuration: 0f);

			state.SetInput(1f, in config);
			state.Advance(0f, in config);
			Assert.That(state.Position, Is.EqualTo(0f));

			state.SetInput(1f, in config);
			state.Advance(1f, in config);
			Assert.That(state.Position, Is.EqualTo(0f));

			state.SetInput(0f, in config);
			state.SetInput(1f, in config);
			Assert.That(state.Position, Is.EqualTo(1f));
		}

		[Test]
		public void ReachedSequenceAdvancesExactlyOncePerArrival()
		{
			var state = CreateState();
			var config = Config(ActuatorCoilMode.FollowCoil, activationDuration: 0f, releaseDuration: 0f);

			state.SetActive(true, in config);
			Assert.That(state.ReachedSequence, Is.EqualTo(1));

			state.SetActive(true, in config);
			Assert.That(state.ReachedSequence, Is.EqualTo(1));

			state.SetActive(false, in config);
			Assert.That(state.ReachedSequence, Is.EqualTo(2));
		}

		[Test]
		public void FollowValueIsExplicitlyProportional()
		{
			var state = CreateState();
			var config = Config(ActuatorCoilMode.FollowValue, activationDuration: 0f, releaseDuration: 0f);

			state.SetInput(0.25f, in config);

			Assert.That(state.Position, Is.EqualTo(0.25f));
			Assert.That(state.TargetPosition, Is.EqualTo(0.25f));
		}

		[Test]
		public void ReversalStartsAtCurrentPoseAndScalesRemainingDuration()
		{
			var state = CreateState();
			var config = Config(ActuatorCoilMode.FollowCoil, activationDuration: 1f, releaseDuration: 1f, releaseDelay: 0f);

			state.SetInput(1f, in config);
			state.Advance(0.4f, in config);
			Assert.That(state.Position, Is.EqualTo(0.4f).Within(0.0001f));

			state.SetInput(0f, in config);
			Assert.That(state.Position, Is.EqualTo(0.4f).Within(0.0001f));
			state.Advance(0.2f, in config);
			Assert.That(state.Position, Is.EqualTo(0.2f).Within(0.0001f));
		}

		[Test]
		public void NullAndKeylessCurvesFallBackToLinear()
		{
			Assert.That(ActuatorMotionState.EvaluateCurve(null, 0.4f), Is.EqualTo(0.4f));
			Assert.That(ActuatorMotionState.EvaluateCurve(new AnimationCurve(), 0.7f), Is.EqualTo(0.7f));
		}

		[Test]
		public void InitialValueCanBeReadBeforeAwake()
		{
			var gameObject = new GameObject("Actuator");
			try {
				var actuator = gameObject.AddComponent<ActuatorComponent>();
				actuator.InitialPosition = 1f;

				Assert.That(((IAnimationValueProvider<float>)actuator).AnimationValue, Is.EqualTo(1f));
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void FollowerAwakeAppliesInitialPositionBeforeFirstFrame()
		{
			var root = new GameObject("Actuator");
			var followerObject = new GameObject("Follower");
			try {
				followerObject.transform.SetParent(root.transform);
				followerObject.transform.localPosition = new Vector3(1f, 0f, 0f);
				var actuator = root.AddComponent<ActuatorComponent>();
				actuator.InitialPosition = 1f;
				var follower = followerObject.AddComponent<ActuatorTransformComponent>();
				follower.PositionOffset = new Vector3(2f, 0f, 0f);

				InvokeLifecycle(follower, "Awake");

				Assert.That(followerObject.transform.localPosition.x, Is.EqualTo(3f).Within(0.0001f));
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void LateEnabledFollowerPullsCurrentActuatorPosition()
		{
			var root = new GameObject("Actuator");
			var followerObject = new GameObject("Follower");
			ActuatorTransformComponent follower = null;
			try {
				followerObject.transform.SetParent(root.transform);
				var actuator = root.AddComponent<ActuatorComponent>();
				follower = followerObject.AddComponent<ActuatorTransformComponent>();
				follower.PositionOffset = new Vector3(2f, 0f, 0f);
				InvokeLifecycle(follower, "Awake");
				InvokeLifecycle(follower, "OnEnable");
				InvokeLifecycle(follower, "OnDisable");

				actuator.SnapTo(1f);
				Assert.That(followerObject.transform.localPosition.x, Is.EqualTo(0f).Within(0.0001f));

				InvokeLifecycle(follower, "OnEnable");

				Assert.That(followerObject.transform.localPosition.x, Is.EqualTo(2f).Within(0.0001f));
			} finally {
				if (follower != null) {
					InvokeLifecycle(follower, "OnDisable");
				}
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void TransformFollowerMapsPositionAndRotation()
		{
			var gameObject = new GameObject("Follower");
			try {
				gameObject.transform.localPosition = new Vector3(1f, 2f, 3f);
				gameObject.transform.localRotation = Quaternion.Euler(0f, 10f, 0f);
				var follower = gameObject.AddComponent<ActuatorTransformComponent>();
				follower.AnimatePosition = true;
				follower.PositionOffset = new Vector3(4f, 0f, 0f);
				follower.AnimateRotation = true;
				follower.RotationOffset = new Vector3(0f, 40f, 0f);
				follower.CaptureInitialPose();

				follower.ApplyValue(0.5f);

				Assert.That(gameObject.transform.localPosition.x, Is.EqualTo(3f).Within(0.0001f));
				Assert.That(Quaternion.Angle(gameObject.transform.localRotation, Quaternion.Euler(0f, 30f, 0f)), Is.LessThan(0.001f));
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void LocalTranslationUsesAuthoredFollowerGizmoAxes()
		{
			var followerObject = new GameObject("Follower");
			try {
				followerObject.transform.localPosition = new Vector3(1f, 2f, 3f);
				followerObject.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
				var follower = followerObject.AddComponent<ActuatorTransformComponent>();
				follower.PositionOffset = new Vector3(0f, 0f, 2f);
				follower.TranslationSpace = ActuatorTranslationSpace.Local;
				follower.CaptureInitialPose();

				follower.ApplyValue(1f);

				Assert.That(Vector3.Distance(followerObject.transform.localPosition, new Vector3(3f, 2f, 3f)), Is.LessThan(0.0001f));
			} finally {
				Object.DestroyImmediate(followerObject);
			}
		}

		[Test]
		public void WorldTranslationStaysWorldAlignedWhenParentMoves()
		{
			var parent = new GameObject("Parent");
			var followerObject = new GameObject("Follower");
			try {
				parent.transform.SetPositionAndRotation(new Vector3(10f, 20f, 0f), Quaternion.Euler(0f, 0f, 90f));
				followerObject.transform.SetParent(parent.transform, false);
				followerObject.transform.localPosition = new Vector3(1f, 0f, 0f);
				var follower = followerObject.AddComponent<ActuatorTransformComponent>();
				follower.PositionOffset = new Vector3(2f, 0f, 0f);
				follower.TranslationSpace = ActuatorTranslationSpace.World;
				follower.CaptureInitialPose();

				follower.ApplyValue(1f);

				Assert.That(Vector3.Distance(followerObject.transform.position, new Vector3(12f, 21f, 0f)), Is.LessThan(0.0001f));

				parent.transform.SetPositionAndRotation(new Vector3(20f, 30f, 0f), Quaternion.Euler(0f, 0f, 180f));
				InvokeLifecycle(follower, "LateUpdate");

				Assert.That(Vector3.Distance(followerObject.transform.position, new Vector3(21f, 30f, 0f)), Is.LessThan(0.0001f));
			} finally {
				Object.DestroyImmediate(parent);
			}
		}

		[Test]
		public void ReverseFollowerUsesOppositeEndpoint()
		{
			var gameObject = new GameObject("Follower");
			try {
				var follower = gameObject.AddComponent<ActuatorTransformComponent>();
				follower.PositionOffset = new Vector3(2f, 0f, 0f);
				follower.Reverse = true;
				follower.CaptureInitialPose();

				follower.ApplyValue(0f);

				Assert.That(gameObject.transform.localPosition.x, Is.EqualTo(2f).Within(0.0001f));
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void TwoFollowersCanUseIndependentGeometry()
		{
			var firstObject = new GameObject("First Follower");
			var secondObject = new GameObject("Second Follower");
			try {
				var first = firstObject.AddComponent<ActuatorTransformComponent>();
				first.PositionOffset = new Vector3(4f, 0f, 0f);
				first.CaptureInitialPose();
				var second = secondObject.AddComponent<ActuatorTransformComponent>();
				second.PositionOffset = new Vector3(0f, 6f, 0f);
				second.ResponseCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0.5f));
				second.CaptureInitialPose();

				first.ApplyValue(1f);
				second.ApplyValue(1f);

				Assert.That(firstObject.transform.localPosition.x, Is.EqualTo(4f).Within(0.0001f));
				Assert.That(secondObject.transform.localPosition.y, Is.EqualTo(3f).Within(0.0001f));
			} finally {
				Object.DestroyImmediate(firstObject);
				Object.DestroyImmediate(secondObject);
			}
		}

		[Test]
		public void EditModePreviewScrubsConnectedFollowersAndRestoresAuthoredPose()
		{
			var root = new GameObject("Actuator Preview");
			var firstObject = new GameObject("First Follower");
			var secondObject = new GameObject("Inactive Follower");
			var actuator = root.AddComponent<ActuatorComponent>();
			try {
				firstObject.transform.localPosition = new Vector3(1f, 0f, 0f);
				firstObject.transform.localRotation = Quaternion.Euler(0f, 10f, 0f);
				var first = firstObject.AddComponent<ActuatorTransformComponent>();
				first._emitter = actuator;
				first.PositionOffset = new Vector3(4f, 0f, 0f);
				first.AnimateRotation = true;
				first.RotationOffset = new Vector3(0f, 40f, 0f);
				var second = secondObject.AddComponent<ActuatorTransformComponent>();
				second._emitter = actuator;
				second.PositionOffset = new Vector3(0f, 6f, 0f);
				second.ResponseCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0.5f));
				secondObject.SetActive(false);

				InvokePreview("Apply", (object)new UnityEngine.Object[] { actuator }, 0.5f);

				Assert.That(firstObject.transform.localPosition.x, Is.EqualTo(3f).Within(0.0001f));
				Assert.That(Quaternion.Angle(firstObject.transform.localRotation, Quaternion.Euler(0f, 30f, 0f)), Is.LessThan(0.001f));
				Assert.That(secondObject.transform.localPosition.y, Is.EqualTo(1.5f).Within(0.0001f));

				InvokePreview("Apply", (object)new UnityEngine.Object[] { actuator }, 0.75f);

				Assert.That(firstObject.transform.localPosition.x, Is.EqualTo(4f).Within(0.0001f));
				Assert.That(secondObject.transform.localPosition.y, Is.EqualTo(6f * second.ResponseCurve.Evaluate(0.75f)).Within(0.0001f));

				InvokePreview("Restore", (object)new UnityEngine.Object[] { actuator });

				Assert.That(firstObject.transform.localPosition, Is.EqualTo(new Vector3(1f, 0f, 0f)));
				Assert.That(Quaternion.Angle(firstObject.transform.localRotation, Quaternion.Euler(0f, 10f, 0f)), Is.LessThan(0.001f));
				Assert.That(secondObject.transform.localPosition, Is.EqualTo(Vector3.zero));
			} finally {
				InvokePreview("Restore", (object)new UnityEngine.Object[] { actuator });
				UnityEngine.Object.DestroyImmediate(root);
				UnityEngine.Object.DestroyImmediate(firstObject);
				UnityEngine.Object.DestroyImmediate(secondObject);
			}
		}

		[Test]
		public void EditModePreviewToleratesDestroyedCachedFollower()
		{
			var root = new GameObject("Actuator Preview");
			var followerObject = new GameObject("Follower");
			var actuator = root.AddComponent<ActuatorComponent>();
			try {
				var follower = followerObject.AddComponent<ActuatorTransformComponent>();
				follower._emitter = actuator;
				follower.PositionOffset = Vector3.right;
				InvokePreview("Apply", (object)new UnityEngine.Object[] { actuator }, 0.5f);
				Object.DestroyImmediate(followerObject);

				Assert.DoesNotThrow(() => InvokePreview("Apply", (object)new UnityEngine.Object[] { actuator }, 0.75f));
			} finally {
				InvokePreview("Restore", (object)new UnityEngine.Object[] { actuator });
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void EditModePreviewSupportsWorldTranslationAxes()
		{
			var root = new GameObject("Actuator Preview");
			var followerObject = new GameObject("Follower");
			var actuator = root.AddComponent<ActuatorComponent>();
			try {
				root.transform.SetPositionAndRotation(new Vector3(10f, 20f, 0f), Quaternion.Euler(0f, 0f, 90f));
				followerObject.transform.SetParent(root.transform, false);
				followerObject.transform.localPosition = new Vector3(1f, 0f, 0f);
				var follower = followerObject.AddComponent<ActuatorTransformComponent>();
				follower._emitter = actuator;
				follower.PositionOffset = new Vector3(2f, 0f, 0f);
				follower.TranslationSpace = ActuatorTranslationSpace.World;

				InvokePreview("Apply", (object)new UnityEngine.Object[] { actuator }, 1f);

				Assert.That(Vector3.Distance(followerObject.transform.position, new Vector3(12f, 21f, 0f)), Is.LessThan(0.0001f));

				root.transform.SetPositionAndRotation(new Vector3(20f, 30f, 0f), Quaternion.Euler(0f, 0f, 180f));
				InvokePreview("MaintainWorldTranslations");

				Assert.That(Vector3.Distance(followerObject.transform.position, new Vector3(21f, 30f, 0f)), Is.LessThan(0.0001f));

				InvokePreview("Restore", (object)new UnityEngine.Object[] { actuator });

				Assert.That(Vector3.Distance(followerObject.transform.localPosition, new Vector3(1f, 0f, 0f)), Is.LessThan(0.0001f));
			} finally {
				InvokePreview("Restore", (object)new UnityEngine.Object[] { actuator });
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void EditModePreviewSupportsFollowerLocalGizmoAxes()
		{
			var root = new GameObject("Actuator Preview");
			var followerObject = new GameObject("Follower");
			var actuator = root.AddComponent<ActuatorComponent>();
			try {
				followerObject.transform.localPosition = new Vector3(1f, 2f, 3f);
				followerObject.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
				var follower = followerObject.AddComponent<ActuatorTransformComponent>();
				follower._emitter = actuator;
				follower.PositionOffset = new Vector3(0f, 0f, 2f);
				follower.TranslationSpace = ActuatorTranslationSpace.Local;

				InvokePreview("Apply", (object)new UnityEngine.Object[] { actuator }, 1f);

				Assert.That(Vector3.Distance(followerObject.transform.localPosition, new Vector3(3f, 2f, 3f)), Is.LessThan(0.0001f));

				InvokePreview("Restore", (object)new UnityEngine.Object[] { actuator });

				Assert.That(Vector3.Distance(followerObject.transform.localPosition, new Vector3(1f, 2f, 3f)), Is.LessThan(0.0001f));
			} finally {
				InvokePreview("Restore", (object)new UnityEngine.Object[] { actuator });
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(followerObject);
			}
		}

		[Test]
		public void EditModePreviewIgnoresPreviewSceneObjects()
		{
			var previewScene = EditorSceneManager.NewPreviewScene();
			var root = new GameObject("Prefab Stage Actuator");
			var followerObject = new GameObject("Prefab Stage Follower");
			var actuator = root.AddComponent<ActuatorComponent>();
			try {
				followerObject.transform.SetParent(root.transform);
				var follower = followerObject.AddComponent<ActuatorTransformComponent>();
				follower._emitter = actuator;
				follower.PositionOffset = new Vector3(4f, 0f, 0f);
				SceneManager.MoveGameObjectToScene(root, previewScene);

				InvokePreview("Apply", (object)new UnityEngine.Object[] { actuator }, 1f);

				Assert.That(followerObject.transform.localPosition, Is.EqualTo(Vector3.zero));
			} finally {
				InvokePreview("Restore", (object)new UnityEngine.Object[] { actuator });
				Object.DestroyImmediate(root);
				EditorSceneManager.ClosePreviewScene(previewScene);
			}
		}

		[Test]
		public void PreviewFallsBackToParentWhenAssignedEmitterHasWrongValueType()
		{
			var root = new GameObject("Actuator");
			var followerObject = new GameObject("Follower");
			var otherObject = new GameObject("Wrong Emitter");
			var actuator = root.AddComponent<ActuatorComponent>();
			try {
				followerObject.transform.SetParent(root.transform);
				var follower = followerObject.AddComponent<ActuatorTransformComponent>();
				follower._emitter = otherObject.AddComponent<TurntableComponent>();
				follower.PositionOffset = new Vector3(3f, 0f, 0f);

				InvokePreview("Apply", (object)new UnityEngine.Object[] { actuator }, 1f);

				Assert.That(followerObject.transform.localPosition.x, Is.EqualTo(3f).Within(0.0001f));
			} finally {
				InvokePreview("Restore", (object)new UnityEngine.Object[] { actuator });
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(otherObject);
			}
		}

		[Test]
		public void ActuatorPackableRoundTripsFieldsAndCurves()
		{
			var gameObject = new GameObject("Actuator");
			try {
				var actuator = gameObject.AddComponent<ActuatorComponent>();
				actuator.CoilMode = ActuatorCoilMode.OneShot;
				actuator.InitialPosition = 0.25f;
				actuator.ActivationDuration = 0.7f;
				actuator.ReleaseDuration = 0.9f;
				actuator.ReleaseDelay = 0.04f;
				actuator.ActivationThreshold = 0.002f;
				actuator.OneShotHoldDuration = 1.2f;
				actuator.ActivationCurve = new AnimationCurve(new Keyframe(0f, 0f, 1f, 2f), new Keyframe(1f, 1f, 3f, 4f));

				var bytes = actuator.Pack();
				actuator.CoilMode = ActuatorCoilMode.FollowCoil;
				actuator.InitialPosition = 0f;
				actuator.ActivationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
				actuator.Unpack(bytes);

				Assert.That(actuator.CoilMode, Is.EqualTo(ActuatorCoilMode.OneShot));
				Assert.That(actuator.InitialPosition, Is.EqualTo(0.25f));
				Assert.That(actuator.ActivationDuration, Is.EqualTo(0.7f));
				Assert.That(actuator.ReleaseDuration, Is.EqualTo(0.9f));
				Assert.That(actuator.ReleaseDelay, Is.EqualTo(0.04f));
				Assert.That(actuator.ActivationThreshold, Is.EqualTo(0.002f));
				Assert.That(actuator.OneShotHoldDuration, Is.EqualTo(1.2f));
				Assert.That(actuator.ActivationCurve.keys[0].outTangent, Is.EqualTo(2f));
				Assert.That(actuator.ActivationCurve.keys[1].inTangent, Is.EqualTo(3f));
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void TransformPackablesRoundTripGeometryAndEmitterReference()
		{
			var root = new GameObject("Table Root");
			var actuatorObject = new GameObject("Actuator");
			var followerObject = new GameObject("Follower");
			try {
				actuatorObject.transform.SetParent(root.transform);
				followerObject.transform.SetParent(root.transform);
				var actuator = actuatorObject.AddComponent<ActuatorComponent>();
				var follower = followerObject.AddComponent<ActuatorTransformComponent>();
				follower._emitter = actuator;
				follower.AnimatePosition = true;
				follower.PositionOffset = new Vector3(1f, 2f, 3f);
				follower.TranslationSpace = ActuatorTranslationSpace.Local;
				follower.AnimateRotation = true;
				follower.RotationOffset = new Vector3(4f, 5f, 6f);
				follower.ResponseCurve = new AnimationCurve(new Keyframe(0f, 0f, 1f, 2f), new Keyframe(1f, 1f, 3f, 4f));
				follower.Reverse = true;

				var refs = new PackagedRefs(root.transform);
				const string actuatorNodeId = "actuator-node";
				refs.SetNodeIdsForWrite(new Dictionary<Transform, string> { { actuatorObject.transform, actuatorNodeId } });
				refs.SetNodeIdsForRead(new Dictionary<string, Transform> { { actuatorNodeId, actuatorObject.transform } });
				var data = follower.Pack();
				var references = follower.PackReferences(root.transform, refs, null);
				follower._emitter = null;
				follower.PositionOffset = Vector3.zero;
				follower.TranslationSpace = ActuatorTranslationSpace.World;
				follower.RotationOffset = Vector3.zero;
				follower.Reverse = false;

				follower.Unpack(data);
				follower.UnpackReferences(references, root.transform, refs, null);

				Assert.That(follower._emitter, Is.SameAs(actuator));
				Assert.That(follower.PositionOffset, Is.EqualTo(new Vector3(1f, 2f, 3f)));
				Assert.That(follower.TranslationSpace, Is.EqualTo(ActuatorTranslationSpace.Local));
				Assert.That(follower.RotationOffset, Is.EqualTo(new Vector3(4f, 5f, 6f)));
				Assert.That(follower.Reverse, Is.True);
				Assert.That(follower.ResponseCurve.keys[0].outTangent, Is.EqualTo(2f));
				Assert.That(follower.ResponseCurve.keys[1].inTangent, Is.EqualTo(3f));
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void ActuatorApiCannotBeInterceptedBySimulationThreadCoilDispatch()
		{
			var gameObject = new GameObject("Actuator");
			try {
				gameObject.AddComponent<ActuatorComponent>();
				var api = new ActuatorApi(gameObject);

				Assert.That(api, Is.Not.InstanceOf<ISimulationThreadCoil>());
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		private static ActuatorMotionState CreateState(float initialPosition = 0f)
		{
			var state = new ActuatorMotionState();
			state.Initialize(initialPosition);
			return state;
		}

		private static ActuatorMotionConfig Config(ActuatorCoilMode mode, float activationDuration = 0.3f, float releaseDuration = 0.3f, float releaseDelay = 0.05f, float oneShotHoldDuration = 0.5f, float activationThreshold = 0.001f)
		{
			return new ActuatorMotionConfig {
				CoilMode = mode,
				ActivationDuration = activationDuration,
				ReleaseDuration = releaseDuration,
				ActivationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
				ReleaseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
				ReleaseDelay = releaseDelay,
				ActivationThreshold = activationThreshold,
				OneShotHoldDuration = oneShotHoldDuration,
			};
		}

		private static void InvokePreview(string methodName, params object[] arguments)
		{
			var previewType = typeof(VisualPinball.Unity.Editor.ActuatorInspector).Assembly.GetType("VisualPinball.Unity.Editor.ActuatorPreview", true);
			var parameterTypes = Array.ConvertAll(arguments, argument => argument.GetType());
			var method = previewType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic, null, parameterTypes, null);
			if (method == null) {
				throw new MissingMethodException(previewType.FullName, methodName);
			}
			method.Invoke(null, arguments);
		}

		private static void InvokeLifecycle(MonoBehaviour component, string methodName)
		{
			var method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
			if (method == null) {
				throw new MissingMethodException(component.GetType().FullName, methodName);
			}
			method.Invoke(component, null);
		}
	}
}
