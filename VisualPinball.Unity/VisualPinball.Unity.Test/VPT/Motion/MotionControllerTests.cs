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
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test.VPT.Motion
{
	public class MotionControllerTests
	{
		[Test]
		public void LegacyPackageNamesResolveRenamedMotionComponents()
		{
			var root = new GameObject("Legacy Motion Package");
			try {
				var refs = new PackagedRefs(root.transform);
				var controllerType = refs.GetType("Actuator");
				var transformType = refs.GetType("ActuatorTransform");
				var controller = root.AddComponent(controllerType);
				var follower = root.AddComponent(transformType);

				Assert.That(controller, Is.TypeOf<MotionControllerComponent>());
				Assert.That(follower, Is.TypeOf<MotionTransformComponent>());
				Assert.That(refs.GetName(controller.GetType()), Is.EqualTo("Actuator"));
				Assert.That(refs.GetName(follower.GetType()), Is.EqualTo("ActuatorTransform"));
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void LegacyCoilMappingDrivesRenamedMotionController()
		{
			var root = new GameObject("Legacy Motion Coil");
			try {
				var controller = root.AddComponent<MotionControllerComponent>();
				controller.ActivationDuration = 0f;
				controller.ReleaseDuration = 0f;
				controller.ReleaseDelay = 0f;
				var api = new MotionControllerApi(root);
				var coil = ((IApiCoilDevice)api).Coil("actuator_coil");

				coil.OnCoil(true);
				Assert.That(controller.Position, Is.EqualTo(1f));
				coil.OnCoil(false);
				Assert.That(controller.Position, Is.Zero);
				Assert.That(controller.AvailableCoils.Single().Id, Is.EqualTo("actuator_coil"));
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void PositionSwitchPreservesAuthoredRangeOrderAndIncludesItsBounds()
		{
			var positionSwitch = new MotionPositionSwitch(MotionPositionSwitchType.EnableBetween, "Home", "home", 0.2f, 0.1f);

			Assert.That(positionSwitch.PositionBeginning, Is.EqualTo(0.2f));
			Assert.That(positionSwitch.PositionEnd, Is.EqualTo(0.1f));
			Assert.That(positionSwitch.Contains(0.1f), Is.True);
			Assert.That(positionSwitch.Contains(0.15f), Is.True);
			Assert.That(positionSwitch.Contains(0.2f), Is.True);
			Assert.That(positionSwitch.Contains(0.21f), Is.False);
		}

		[Test]
		public void PositionSwitchCountsPulseMarksInBothDirections()
		{
			var positionSwitch = new MotionPositionSwitch(MotionPositionSwitchType.PulseBetween, "Encoder", "encoder", 0.4f, 0.6f, 0.1f);

			Assert.That(positionSwitch.CountPulses(0.35f, 0.65f), Is.EqualTo(3));
			Assert.That(positionSwitch.CountPulses(0.65f, 0.35f), Is.EqualTo(3));
			Assert.That(positionSwitch.CountPulses(0.5f, 0.5f), Is.Zero);
		}

		[Test]
		public void AlwaysPulseUsesFullStrokeAndIsPathIndependent()
		{
			var positionSwitch = new MotionPositionSwitch(MotionPositionSwitchType.AlwaysPulse, "Encoder", "encoder", 0.4f, 0.6f, 0.1f);

			Assert.That(positionSwitch.CountPulses(0.05f, 0.15f), Is.EqualTo(1));
			Assert.That(positionSwitch.CountPulses(0f, 1f), Is.EqualTo(10));
			Assert.That(positionSwitch.CountPulses(0f, 0.5f) + positionSwitch.CountPulses(0.5f, 1f), Is.EqualTo(10));
			Assert.That(positionSwitch.CountPulses(1f, 0f), Is.EqualTo(10));
			Assert.That(positionSwitch.CountPulses(1f, 0.5f) + positionSwitch.CountPulses(0.5f, 0f), Is.EqualTo(10));
		}

		[Test]
		public void MotionControllerPublishesConfiguredSwitchItems()
		{
			var gameObject = new GameObject("Motion Controller");
			try {
				var motionController = gameObject.AddComponent<MotionControllerComponent>();
				motionController.Switches = new[] {
					new MotionPositionSwitch(MotionPositionSwitchType.EnableBetween, "Home", "home", 0f, 0.01f),
					new MotionPositionSwitch(MotionPositionSwitchType.AlwaysPulse, "Encoder", "encoder", 0f, 1f),
					new MotionPositionSwitch(MotionPositionSwitchType.EnableBetween, "Invalid", "", 0f, 1f),
				};

				var switches = motionController.AvailableSwitches.ToArray();

				Assert.That(switches.Select(item => item.Id), Is.EqualTo(new[] { "home", "encoder" }));
				Assert.That(switches.Select(item => item.Description), Is.EqualTo(new[] { "Home", "Encoder" }));
				Assert.That(switches.Select(item => item.IsPulseSwitch), Is.EqualTo(new[] { false, false }));
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void MotionControllerSerializationPreservesCustomNamesWhenMakingThemUnique()
		{
			var gameObject = new GameObject("Motion Controller");
			try {
				var motionController = gameObject.AddComponent<MotionControllerComponent>();
				motionController.Switches = new[] {
					new MotionPositionSwitch(MotionPositionSwitchType.EnableBetween, "Left Limit", "first", 0f, 0.1f),
					new MotionPositionSwitch(MotionPositionSwitchType.EnableBetween, "Left Limit", "second", 0.9f, 1f),
					new MotionPositionSwitch(MotionPositionSwitchType.AlwaysPulse, "", "encoder", 0f, 1f),
				};

				motionController.OnBeforeSerialize();

				Assert.That(motionController.Switches.Select(positionSwitch => positionSwitch.Name), Is.EqualTo(new[] { "Left Limit", "Left Limit 2", "Position Switch" }));
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void MotionControllerApiTracksMaintainedPositionRanges()
		{
			var root = new GameObject("Table");
			var motionControllerObject = new GameObject("Motion Controller");
			try {
				motionControllerObject.transform.SetParent(root.transform);
				var player = root.AddComponent<Player>();
				var motionController = motionControllerObject.AddComponent<MotionControllerComponent>();
				motionController.Switches = new[] {
					new MotionPositionSwitch(MotionPositionSwitchType.EnableBetween, "Home", "home", 0f, 0.01f),
					new MotionPositionSwitch(MotionPositionSwitchType.EnableBetween, "End", "end", 0.99f, 1f),
				};
				var api = new MotionControllerApi(motionControllerObject, player);
				var home = api.Switch("home");
				var end = api.Switch("end");
				Assert.That(api.Switch("missing"), Is.Null);

				api.UpdateSwitches(0f, 0f, false, true);
				Assert.That(home.IsSwitchEnabled, Is.True);
				Assert.That(end.IsSwitchEnabled, Is.False);

				api.UpdateSwitches(0f, 0.5f, true);
				Assert.That(home.IsSwitchEnabled, Is.False);
				Assert.That(end.IsSwitchEnabled, Is.False);

				api.UpdateSwitches(0.5f, 1f, true);
				Assert.That(home.IsSwitchEnabled, Is.False);
				Assert.That(end.IsSwitchEnabled, Is.True);
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void MotionControllerApiQueuesDistinctPulseEdgesCrossedInOneUpdate()
		{
			var gameObject = new GameObject("Motion Controller");
			try {
				var player = gameObject.AddComponent<Player>();
				var motionController = gameObject.AddComponent<MotionControllerComponent>();
				motionController.Switches = new[] {
					new MotionPositionSwitch(MotionPositionSwitchType.PulseBetween, "Encoder", "encoder", 0.4f, 0.6f, 0.1f, 10),
				};
				var api = new MotionControllerApi(gameObject, player);
				var encoder = api.Switch("encoder");
				var callIndex = 0;
				var edges = new List<(int CallIndex, bool IsEnabled)>();
				encoder.Switch += (_, args) => edges.Add((callIndex, args.IsEnabled));

				callIndex = 1;
				api.UpdateSwitches(0.35f, 0.65f, true);
				for (callIndex = 2; callIndex <= 6; callIndex++) {
					api.AdvancePulses(0.1f);
				}

				Assert.That(edges, Is.EqualTo(new[] {
					(1, true), (2, false), (3, true), (4, false), (5, true), (6, false),
				}));
				Assert.That(encoder.IsSwitchEnabled, Is.False);
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void RestoringPositionCancelsQueuedPulsesAndOpensActivePulse()
		{
			var gameObject = new GameObject("Motion Controller");
			try {
				var player = gameObject.AddComponent<Player>();
				var motionController = gameObject.AddComponent<MotionControllerComponent>();
				motionController.Switches = new[] {
					new MotionPositionSwitch(MotionPositionSwitchType.PulseBetween, "Encoder", "encoder", 0.4f, 0.6f, 0.1f, 10),
				};
				var api = new MotionControllerApi(gameObject, player);
				var encoder = api.Switch("encoder");
				var states = new List<bool>();
				encoder.Switch += (_, args) => states.Add(args.IsEnabled);

				api.UpdateSwitches(0.35f, 0.65f, true);
				api.UpdateSwitches(0.65f, 0.2f, false, true, true);
				api.AdvancePulses(1f);

				Assert.That(states, Is.EqualTo(new[] { true, false }));
				Assert.That(encoder.IsSwitchEnabled, Is.False);
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void RepeatedNonzeroSamplesToggleOnlyOnce()
		{
			var state = CreateState();
			var config = Config(MotionCoilMode.ToggleOnPulse, activationDuration: 0f, releaseDuration: 0f);

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
			var config = Config(MotionCoilMode.ToggleOnPulse, activationDuration: 0f, releaseDuration: 0f, releaseDelay: 0.05f);

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
			var config = Config(MotionCoilMode.ToggleOnPulse, activationDuration: 0f, releaseDuration: 0f, releaseDelay: 0.05f);

			state.SetInput(1f, in config);
			state.SetInput(0f, in config);
			state.Advance(0.05f, in config);
			state.SetInput(1f, in config);

			Assert.That(state.Position, Is.EqualTo(0f));
			Assert.That(state.TargetPosition, Is.EqualTo(0f));
		}

		[Test]
		public void ReducedStrengthInputIsOneBinaryActivationNotPosition()
		{
			var state = CreateState();
			var config = Config(MotionCoilMode.ToggleOnPulse, activationDuration: 0f, releaseDuration: 0f);

			state.SetInput(64f / 255f, in config);

			Assert.That(state.Position, Is.EqualTo(1f));
			Assert.That(state.Position, Is.Not.EqualTo(64f / 255f));
		}

		[Test]
		public void ValuesAtOrBelowActivationThresholdDoNotActivate()
		{
			var state = CreateState();
			var config = Config(MotionCoilMode.ToggleOnPulse, activationDuration: 0f, releaseDuration: 0f, activationThreshold: 0.2f);

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
			var config = Config(MotionCoilMode.FollowCoil, activationDuration: 0f, releaseDuration: 0f, releaseDelay: 0.05f);

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
			var config = Config(MotionCoilMode.FollowCoil, activationDuration: 0f, releaseDuration: 0f, releaseDelay: 0.05f);

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
			var config = Config(MotionCoilMode.OneShot, activationDuration: 0.1f, releaseDuration: 0.1f, oneShotHoldDuration: 0.2f);

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
			var config = Config(MotionCoilMode.OneShot, activationDuration: 0f, releaseDuration: 0f, releaseDelay: 0f, oneShotHoldDuration: 0f);

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
			var config = Config(MotionCoilMode.FollowCoil, activationDuration: 0f, releaseDuration: 0f);

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
			var config = Config(MotionCoilMode.FollowValue, activationDuration: 0f, releaseDuration: 0f);

			state.SetInput(0.25f, in config);

			Assert.That(state.Position, Is.EqualTo(0.25f));
			Assert.That(state.TargetPosition, Is.EqualTo(0.25f));
		}

		[Test]
		public void ArbitraryPositionCommandScalesTravelAndCanRetarget()
		{
			var state = CreateState();
			var config = Config(MotionCoilMode.FollowCoil, activationDuration: 2f, releaseDuration: 1f);

			state.MoveToPosition(0.75f, in config);
			state.Advance(0.75f, in config);
			Assert.That(state.Position, Is.EqualTo(0.375f).Within(0.0001f));

			state.MoveToPosition(0.125f, in config);
			state.Advance(0.25f, in config);

			Assert.That(state.Position, Is.EqualTo(0.125f).Within(0.0001f));
			Assert.That(state.TargetPosition, Is.EqualTo(0.125f));
			Assert.That(state.IsMoving, Is.False);
		}

		[Test]
		public void MotionControllerApiAcceptsArbitraryPositionCommand()
		{
			var gameObject = new GameObject("Motion Controller");
			try {
				var motionController = gameObject.AddComponent<MotionControllerComponent>();
				motionController.ActivationDuration = 0f;
				var api = new MotionControllerApi(gameObject);

				api.MoveTo(0.4f);

				Assert.That(api.Position, Is.EqualTo(0.4f));
				Assert.That(api.TargetPosition, Is.EqualTo(0.4f));
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void ReversalStartsAtCurrentPoseAndScalesRemainingDuration()
		{
			var state = CreateState();
			var config = Config(MotionCoilMode.FollowCoil, activationDuration: 1f, releaseDuration: 1f, releaseDelay: 0f);

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
			Assert.That(MotionState.EvaluateCurve(null, 0.4f), Is.EqualTo(0.4f));
			Assert.That(MotionState.EvaluateCurve(new AnimationCurve(), 0.7f), Is.EqualTo(0.7f));
		}

		[Test]
		public void InitialValueCanBeReadBeforeAwake()
		{
			var gameObject = new GameObject("Motion Controller");
			try {
				var motionController = gameObject.AddComponent<MotionControllerComponent>();
				motionController.InitialPosition = 1f;

				Assert.That(((IAnimationValueProvider<float>)motionController).AnimationValue, Is.EqualTo(1f));
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void FollowerAwakeAppliesInitialPositionBeforeFirstFrame()
		{
			var root = new GameObject("Motion Controller");
			var followerObject = new GameObject("Follower");
			try {
				followerObject.transform.SetParent(root.transform);
				followerObject.transform.localPosition = new Vector3(1f, 0f, 0f);
				var motionController = root.AddComponent<MotionControllerComponent>();
				motionController.InitialPosition = 1f;
				var follower = followerObject.AddComponent<MotionTransformComponent>();
				follower.PositionOffset = new Vector3(2f, 0f, 0f);

				InvokeLifecycle(follower, "Awake");

				Assert.That(followerObject.transform.localPosition.x, Is.EqualTo(3f).Within(0.0001f));
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void LateEnabledFollowerPullsCurrentMotionControllerPosition()
		{
			var root = new GameObject("Motion Controller");
			var followerObject = new GameObject("Follower");
			MotionTransformComponent follower = null;
			try {
				followerObject.transform.SetParent(root.transform);
				var motionController = root.AddComponent<MotionControllerComponent>();
				follower = followerObject.AddComponent<MotionTransformComponent>();
				follower.PositionOffset = new Vector3(2f, 0f, 0f);
				InvokeLifecycle(follower, "Awake");
				InvokeLifecycle(follower, "OnEnable");
				InvokeLifecycle(follower, "OnDisable");

				motionController.SnapTo(1f);
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
				var follower = gameObject.AddComponent<MotionTransformComponent>();
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
				var follower = followerObject.AddComponent<MotionTransformComponent>();
				follower.PositionOffset = new Vector3(0f, 0f, 2f);
				follower.TranslationSpace = MotionTranslationSpace.Local;
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
				var follower = followerObject.AddComponent<MotionTransformComponent>();
				follower.PositionOffset = new Vector3(2f, 0f, 0f);
				follower.TranslationSpace = MotionTranslationSpace.World;
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
				var follower = gameObject.AddComponent<MotionTransformComponent>();
				follower.PositionOffset = new Vector3(2f, 0f, 0f);
				follower.Reverse = true;
				follower.CaptureInitialPose();

				follower.ApplyValue(0f);

				Assert.That(gameObject.transform.localPosition.x, Is.EqualTo(2f).Within(0.0001f));
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[TestCase(0f, false, 0f)]
		[TestCase(0.25f, false, 0f)]
		[TestCase(0.375f, false, 9.375f)]
		[TestCase(0.5f, false, 30f)]
		[TestCase(0.75f, false, 60f)]
		[TestCase(1f, false, 60f)]
		[TestCase(0.25f, true, 60f)]
		[TestCase(0.75f, true, 0f)]
		public void FollowerMapsInputWindowThroughCurveToRotation(float position, bool reverse, float expectedAngle)
		{
			var go = new GameObject("Gate pivot");
			try {
				var follower = go.AddComponent<MotionTransformComponent>();
				go.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
				var initial = go.transform.localRotation;
				follower.AnimatePosition = false;
				follower.AnimateRotation = true;
				follower.RotationOffset = new Vector3(60f, 0f, 0f);
				follower.InputMin = 0.25f;
				follower.InputMax = 0.75f;
				follower.ResponseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
				follower.Reverse = reverse;
				follower.CaptureInitialPose();
				follower.ApplyValue(1f);
				follower.ApplyValue(position);
				Assert.That(Quaternion.Angle(go.transform.localRotation,
					initial * Quaternion.Euler(expectedAngle, 0f, 0f)), Is.LessThan(0.05f));
			} finally { Object.DestroyImmediate(go); }
		}

		[TestCase(0.5f, 0.5f)]
		[TestCase(0.75f, 0.25f)]
		[TestCase(-0.1f, 1f)]
		[TestCase(0f, 1.1f)]
		[TestCase(float.NaN, 1f)]
		public void InvalidFollowerRangeRetainsAuthoredPose(float min, float max)
		{
			var go = new GameObject("Follower");
			try {
				var follower = go.AddComponent<MotionTransformComponent>();
				follower.InputMin = min;
				follower.InputMax = max;
				follower.Reverse = true;
				follower.PositionOffset = Vector3.up;
				follower.ApplyValue(0.75f);
				Assert.That(follower.HasValidInputRange, Is.False);
				Assert.That(go.transform.localPosition, Is.EqualTo(Vector3.zero));
			} finally { Object.DestroyImmediate(go); }
		}

		[Test]
		public void PreviewUsesSamePartialRangeAsRuntimeIncludingWorldPositionMaintenance()
		{
			var root = new GameObject("Range preview");
			try {
				var motionController = root.AddComponent<MotionControllerComponent>();
				var go = new GameObject("Follower");
				go.transform.SetParent(root.transform);
				var follower = go.AddComponent<MotionTransformComponent>();
				follower._emitter = motionController;
				follower.InputMin = 0.5f;
				follower.InputMax = 0.75f;
				follower.PositionOffset = Vector3.up * 4f;
				follower.AnimateRotation = true;
				follower.RotationOffset = new Vector3(60f, 0f, 0f);
				follower.Reverse = true;
				InvokePreview("Apply", (object)new UnityEngine.Object[] { motionController }, 0.5625f);
				InvokePreview("MaintainWorldTranslations");
				var previewPosition = go.transform.localPosition;
				var previewRotation = go.transform.localRotation;
				Assert.That(previewPosition.y, Is.EqualTo(3f).Within(0.0001f));
				InvokePreview("Restore", (object)new UnityEngine.Object[] { motionController });
				Assert.That(go.transform.localPosition, Is.EqualTo(Vector3.zero));
				follower.CaptureInitialPose();
				follower.ApplyValue(0.5625f);
				Assert.That(go.transform.localPosition, Is.EqualTo(previewPosition));
				Assert.That(Quaternion.Angle(go.transform.localRotation, previewRotation), Is.LessThan(0.05f));
			} finally {
				InvokePreview("RestoreAll");
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void OldFollowerPackageWithoutRangeRestoresFullTravel()
		{
			var go = new GameObject("Legacy follower");
			try {
				var follower = go.AddComponent<MotionTransformComponent>();
				follower.InputMin = 0.25f;
				follower.InputMax = 0.5f;
				follower.Unpack(PackageApi.Packer.Pack(new { AnimatePosition = true }));
				Assert.That(follower.InputMin, Is.Zero);
				Assert.That(follower.InputMax, Is.EqualTo(1f));
				Assert.That(follower.EvaluateFactor(0.75f), Is.EqualTo(0.75f));
			} finally { Object.DestroyImmediate(go); }
		}

		[Test]
		public void TwoFollowersCanUseIndependentGeometry()
		{
			var firstObject = new GameObject("First Follower");
			var secondObject = new GameObject("Second Follower");
			try {
				var first = firstObject.AddComponent<MotionTransformComponent>();
				first.PositionOffset = new Vector3(4f, 0f, 0f);
				first.CaptureInitialPose();
				var second = secondObject.AddComponent<MotionTransformComponent>();
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
			var root = new GameObject("Motion Controller Preview");
			var firstObject = new GameObject("First Follower");
			var secondObject = new GameObject("Inactive Follower");
			var motionController = root.AddComponent<MotionControllerComponent>();
			try {
				firstObject.transform.localPosition = new Vector3(1f, 0f, 0f);
				firstObject.transform.localRotation = Quaternion.Euler(0f, 10f, 0f);
				var first = firstObject.AddComponent<MotionTransformComponent>();
				first._emitter = motionController;
				first.PositionOffset = new Vector3(4f, 0f, 0f);
				first.AnimateRotation = true;
				first.RotationOffset = new Vector3(0f, 40f, 0f);
				var second = secondObject.AddComponent<MotionTransformComponent>();
				second._emitter = motionController;
				second.PositionOffset = new Vector3(0f, 6f, 0f);
				second.ResponseCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0.5f));
				secondObject.SetActive(false);

				InvokePreview("Apply", (object)new UnityEngine.Object[] { motionController }, 0.5f);

				Assert.That(firstObject.transform.localPosition.x, Is.EqualTo(3f).Within(0.0001f));
				Assert.That(Quaternion.Angle(firstObject.transform.localRotation, Quaternion.Euler(0f, 30f, 0f)), Is.LessThan(0.001f));
				Assert.That(secondObject.transform.localPosition.y, Is.EqualTo(1.5f).Within(0.0001f));

				InvokePreview("Apply", (object)new UnityEngine.Object[] { motionController }, 0.75f);

				Assert.That(firstObject.transform.localPosition.x, Is.EqualTo(4f).Within(0.0001f));
				Assert.That(secondObject.transform.localPosition.y, Is.EqualTo(6f * second.ResponseCurve.Evaluate(0.75f)).Within(0.0001f));

				InvokePreview("Restore", (object)new UnityEngine.Object[] { motionController });

				Assert.That(firstObject.transform.localPosition, Is.EqualTo(new Vector3(1f, 0f, 0f)));
				Assert.That(Quaternion.Angle(firstObject.transform.localRotation, Quaternion.Euler(0f, 10f, 0f)), Is.LessThan(0.001f));
				Assert.That(secondObject.transform.localPosition, Is.EqualTo(Vector3.zero));
			} finally {
				InvokePreview("Restore", (object)new UnityEngine.Object[] { motionController });
				UnityEngine.Object.DestroyImmediate(root);
				UnityEngine.Object.DestroyImmediate(firstObject);
				UnityEngine.Object.DestroyImmediate(secondObject);
			}
		}

		[Test]
		public void EditModePreviewToleratesDestroyedCachedFollower()
		{
			var root = new GameObject("Motion Controller Preview");
			var followerObject = new GameObject("Follower");
			var motionController = root.AddComponent<MotionControllerComponent>();
			try {
				var follower = followerObject.AddComponent<MotionTransformComponent>();
				follower._emitter = motionController;
				follower.PositionOffset = Vector3.right;
				InvokePreview("Apply", (object)new UnityEngine.Object[] { motionController }, 0.5f);
				Object.DestroyImmediate(followerObject);

				Assert.DoesNotThrow(() => InvokePreview("Apply", (object)new UnityEngine.Object[] { motionController }, 0.75f));
			} finally {
				InvokePreview("Restore", (object)new UnityEngine.Object[] { motionController });
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void EditModePreviewSupportsWorldTranslationAxes()
		{
			var root = new GameObject("Motion Controller Preview");
			var followerObject = new GameObject("Follower");
			var motionController = root.AddComponent<MotionControllerComponent>();
			try {
				root.transform.SetPositionAndRotation(new Vector3(10f, 20f, 0f), Quaternion.Euler(0f, 0f, 90f));
				followerObject.transform.SetParent(root.transform, false);
				followerObject.transform.localPosition = new Vector3(1f, 0f, 0f);
				var follower = followerObject.AddComponent<MotionTransformComponent>();
				follower._emitter = motionController;
				follower.PositionOffset = new Vector3(2f, 0f, 0f);
				follower.TranslationSpace = MotionTranslationSpace.World;

				InvokePreview("Apply", (object)new UnityEngine.Object[] { motionController }, 1f);

				Assert.That(Vector3.Distance(followerObject.transform.position, new Vector3(12f, 21f, 0f)), Is.LessThan(0.0001f));

				root.transform.SetPositionAndRotation(new Vector3(20f, 30f, 0f), Quaternion.Euler(0f, 0f, 180f));
				InvokePreview("MaintainWorldTranslations");

				Assert.That(Vector3.Distance(followerObject.transform.position, new Vector3(21f, 30f, 0f)), Is.LessThan(0.0001f));

				InvokePreview("Restore", (object)new UnityEngine.Object[] { motionController });

				Assert.That(Vector3.Distance(followerObject.transform.localPosition, new Vector3(1f, 0f, 0f)), Is.LessThan(0.0001f));
			} finally {
				InvokePreview("Restore", (object)new UnityEngine.Object[] { motionController });
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void EditModePreviewSupportsFollowerLocalGizmoAxes()
		{
			var root = new GameObject("Motion Controller Preview");
			var followerObject = new GameObject("Follower");
			var motionController = root.AddComponent<MotionControllerComponent>();
			try {
				followerObject.transform.localPosition = new Vector3(1f, 2f, 3f);
				followerObject.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
				var follower = followerObject.AddComponent<MotionTransformComponent>();
				follower._emitter = motionController;
				follower.PositionOffset = new Vector3(0f, 0f, 2f);
				follower.TranslationSpace = MotionTranslationSpace.Local;

				InvokePreview("Apply", (object)new UnityEngine.Object[] { motionController }, 1f);

				Assert.That(Vector3.Distance(followerObject.transform.localPosition, new Vector3(3f, 2f, 3f)), Is.LessThan(0.0001f));

				InvokePreview("Restore", (object)new UnityEngine.Object[] { motionController });

				Assert.That(Vector3.Distance(followerObject.transform.localPosition, new Vector3(1f, 2f, 3f)), Is.LessThan(0.0001f));
			} finally {
				InvokePreview("Restore", (object)new UnityEngine.Object[] { motionController });
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(followerObject);
			}
		}

		[Test]
		public void EditModePreviewIgnoresPreviewSceneObjects()
		{
			var previewScene = EditorSceneManager.NewPreviewScene();
			var root = new GameObject("Prefab Stage Motion Controller");
			var followerObject = new GameObject("Prefab Stage Follower");
			var motionController = root.AddComponent<MotionControllerComponent>();
			try {
				followerObject.transform.SetParent(root.transform);
				var follower = followerObject.AddComponent<MotionTransformComponent>();
				follower._emitter = motionController;
				follower.PositionOffset = new Vector3(4f, 0f, 0f);
				SceneManager.MoveGameObjectToScene(root, previewScene);

				InvokePreview("Apply", (object)new UnityEngine.Object[] { motionController }, 1f);

				Assert.That(followerObject.transform.localPosition, Is.EqualTo(Vector3.zero));
			} finally {
				InvokePreview("Restore", (object)new UnityEngine.Object[] { motionController });
				Object.DestroyImmediate(root);
				EditorSceneManager.ClosePreviewScene(previewScene);
			}
		}

		[Test]
		public void PreviewFallsBackToParentWhenAssignedEmitterHasWrongValueType()
		{
			var root = new GameObject("Motion Controller");
			var followerObject = new GameObject("Follower");
			var otherObject = new GameObject("Wrong Emitter");
			var motionController = root.AddComponent<MotionControllerComponent>();
			try {
				followerObject.transform.SetParent(root.transform);
				var follower = followerObject.AddComponent<MotionTransformComponent>();
				follower._emitter = otherObject.AddComponent<TurntableComponent>();
				follower.PositionOffset = new Vector3(3f, 0f, 0f);

				InvokePreview("Apply", (object)new UnityEngine.Object[] { motionController }, 1f);

				Assert.That(followerObject.transform.localPosition.x, Is.EqualTo(3f).Within(0.0001f));
			} finally {
				InvokePreview("Restore", (object)new UnityEngine.Object[] { motionController });
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(otherObject);
			}
		}

		[Test]
		public void MotionControllerPackableRoundTripsFieldsAndCurves()
		{
			var gameObject = new GameObject("Motion Controller");
			try {
				var motionController = gameObject.AddComponent<MotionControllerComponent>();
				motionController.CoilMode = MotionCoilMode.OneShot;
				motionController.InitialPosition = 0.25f;
				motionController.ActivationDuration = 0.7f;
				motionController.ReleaseDuration = 0.9f;
				motionController.ReleaseDelay = 0.04f;
				motionController.ActivationThreshold = 0.002f;
				motionController.OneShotHoldDuration = 1.2f;
				motionController.ActivationCurve = new AnimationCurve(new Keyframe(0f, 0f, 1f, 2f), new Keyframe(1f, 1f, 3f, 4f));
				motionController.Switches = new[] {
					new MotionPositionSwitch(MotionPositionSwitchType.EnableBetween, "Home", "home", 0f, 0.02f),
					new MotionPositionSwitch(MotionPositionSwitchType.PulseBetween, "Encoder", "encoder", 0.2f, 0.8f, 0.05f, 15),
				};

				var bytes = motionController.Pack();
				motionController.CoilMode = MotionCoilMode.FollowCoil;
				motionController.InitialPosition = 0f;
				motionController.ActivationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
				motionController.Switches = Array.Empty<MotionPositionSwitch>();
				motionController.Unpack(bytes);

				Assert.That(motionController.CoilMode, Is.EqualTo(MotionCoilMode.OneShot));
				Assert.That(motionController.InitialPosition, Is.EqualTo(0.25f));
				Assert.That(motionController.ActivationDuration, Is.EqualTo(0.7f));
				Assert.That(motionController.ReleaseDuration, Is.EqualTo(0.9f));
				Assert.That(motionController.ReleaseDelay, Is.EqualTo(0.04f));
				Assert.That(motionController.ActivationThreshold, Is.EqualTo(0.002f));
				Assert.That(motionController.OneShotHoldDuration, Is.EqualTo(1.2f));
				Assert.That(motionController.ActivationCurve.keys[0].outTangent, Is.EqualTo(2f));
				Assert.That(motionController.ActivationCurve.keys[1].inTangent, Is.EqualTo(3f));
				Assert.That(motionController.Switches, Has.Length.EqualTo(2));
				Assert.That(motionController.Switches[0].SwitchId, Is.EqualTo("home"));
				Assert.That(motionController.Switches[0].PositionEnd, Is.EqualTo(0.02f));
				Assert.That(motionController.Switches[1].Type, Is.EqualTo(MotionPositionSwitchType.PulseBetween));
				Assert.That(motionController.Switches[1].PulseInterval, Is.EqualTo(0.05f));
				Assert.That(motionController.Switches[1].PulseDuration, Is.EqualTo(15));
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void TransformPackablesRoundTripGeometryAndEmitterReference()
		{
			var root = new GameObject("Table Root");
			var motionControllerObject = new GameObject("Motion Controller");
			var followerObject = new GameObject("Follower");
			try {
				motionControllerObject.transform.SetParent(root.transform);
				followerObject.transform.SetParent(root.transform);
				var motionController = motionControllerObject.AddComponent<MotionControllerComponent>();
				var follower = followerObject.AddComponent<MotionTransformComponent>();
				follower._emitter = motionController;
				follower.AnimatePosition = true;
				follower.PositionOffset = new Vector3(1f, 2f, 3f);
				follower.TranslationSpace = MotionTranslationSpace.Local;
				follower.AnimateRotation = true;
				follower.RotationOffset = new Vector3(4f, 5f, 6f);
				follower.InputMin = 0.2f;
				follower.InputMax = 0.8f;
				follower.ResponseCurve = new AnimationCurve(new Keyframe(0f, 0f, 1f, 2f), new Keyframe(1f, 1f, 3f, 4f));
				follower.Reverse = true;

				var refs = new PackagedRefs(root.transform);
				const string motionControllerNodeId = "motion-controller-node";
				refs.SetNodeIdsForWrite(new Dictionary<Transform, string> { { motionControllerObject.transform, motionControllerNodeId } });
				refs.SetNodeIdsForRead(new Dictionary<string, Transform> { { motionControllerNodeId, motionControllerObject.transform } });
				var data = follower.Pack();
				var references = follower.PackReferences(root.transform, refs, null);
				follower._emitter = null;
				follower.PositionOffset = Vector3.zero;
				follower.TranslationSpace = MotionTranslationSpace.World;
				follower.RotationOffset = Vector3.zero;
				follower.InputMin = 0f;
				follower.InputMax = 1f;
				follower.Reverse = false;

				follower.Unpack(data);
				follower.UnpackReferences(references, root.transform, refs, null);

				Assert.That(follower._emitter, Is.SameAs(motionController));
				Assert.That(follower.PositionOffset, Is.EqualTo(new Vector3(1f, 2f, 3f)));
				Assert.That(follower.TranslationSpace, Is.EqualTo(MotionTranslationSpace.Local));
				Assert.That(follower.RotationOffset, Is.EqualTo(new Vector3(4f, 5f, 6f)));
				Assert.That(follower.Reverse, Is.True);
				Assert.That(follower.InputMin, Is.EqualTo(0.2f));
				Assert.That(follower.InputMax, Is.EqualTo(0.8f));
				Assert.That(follower.ResponseCurve.keys[0].outTangent, Is.EqualTo(2f));
				Assert.That(follower.ResponseCurve.keys[1].inTangent, Is.EqualTo(3f));
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void MotionControllerApiCannotBeInterceptedBySimulationThreadCoilDispatch()
		{
			var gameObject = new GameObject("Motion Controller");
			try {
				gameObject.AddComponent<MotionControllerComponent>();
				var api = new MotionControllerApi(gameObject);

				Assert.That(api, Is.Not.InstanceOf<ISimulationThreadCoil>());
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		private static MotionState CreateState(float initialPosition = 0f)
		{
			var state = new MotionState();
			state.Initialize(initialPosition);
			return state;
		}

		private static MotionConfig Config(MotionCoilMode mode, float activationDuration = 0.3f, float releaseDuration = 0.3f, float releaseDelay = 0.05f, float oneShotHoldDuration = 0.5f, float activationThreshold = 0.001f)
		{
			return new MotionConfig {
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
			var previewType = typeof(VisualPinball.Unity.Editor.MotionControllerInspector).Assembly.GetType("VisualPinball.Unity.Editor.MotionPreview", true);
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
