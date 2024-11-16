using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	internal static class PhysicsMovements
	{

		internal static void ApplyBallMovement(ref PhysicsState state, Dictionary<int, Transform> transforms)
		{
			using var enumerator = state.Balls.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var ball = ref enumerator.Current.Value;
				BallMovementPhysics.Move(ball, transforms[ball.Id]);
			}
		}

		internal static void ApplyFlipperMovement(ref NativeParallelHashMap<int, FlipperState> flipperStates,
			in Transform transform, Dictionary<int, Transform> transforms)
		{
			using var enumerator = flipperStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var flipperState = ref enumerator.Current.Value;
				var flipperTransform = transforms[enumerator.Current.Key];
				var currentRotation = transform.localEulerAngles;
				currentRotation.y = math.degrees(flipperState.Movement.Angle);
				flipperTransform.localEulerAngles = currentRotation;
			}
		}

		internal static void ApplyBumperMovement(ref NativeParallelHashMap<int, BumperState> bumperStates, Dictionary<int, Transform> transforms)
		{
			using var enumerator = bumperStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var bumperState = ref enumerator.Current.Value;
				if (bumperState.SkirtItemId != 0) {
					BumperTransform.UpdateSkirt(in bumperState.SkirtAnimation, transforms[bumperState.SkirtItemId]);
				}
				if (bumperState.RingItemId != 0) {
					BumperTransform.UpdateRing(bumperState.RingItemId, in bumperState.RingAnimation, transforms[bumperState.RingItemId]);
				}
			}
		}

		internal static void ApplyDropTargetMovement(ref NativeParallelHashMap<int, DropTargetState> dropTargetStates, Dictionary<int, Transform> transforms)
		{
			using var enumerator = dropTargetStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var dropTargetState = ref enumerator.Current.Value;
				var dropTargetTransform = transforms[dropTargetState.AnimatedItemId];
				var localPos = dropTargetTransform.localPosition;
				dropTargetTransform.localPosition = new Vector3(
					localPos.x,
					Physics.ScaleToWorld(dropTargetState.Animation.ZOffset),
					localPos.z
				);
			}
		}

		internal static void ApplyHitTargetMovement(ref NativeParallelHashMap<int, HitTargetState> hitTargetStates, Dictionary<int, Transform> transforms)
		{
			using var enumerator = hitTargetStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var hitTargetState = ref enumerator.Current.Value;
				var hitTargetTransform = transforms[hitTargetState.AnimatedItemId];
				var localRot = hitTargetTransform.localEulerAngles;
				hitTargetTransform.localEulerAngles = new Vector3(
					hitTargetState.Animation.XRotation,
					localRot.y,
					localRot.z
				);
			}
		}

		internal static void ApplyGateMovement(ref NativeParallelHashMap<int, GateState> gateStates,
			Dictionary<int, IRotatableAnimationComponent> rotatableComponent)
		{
			using var enumerator = gateStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var gateState = ref enumerator.Current.Value;
				var component = rotatableComponent[enumerator.Current.Key];
				component.OnRotationUpdated(gateState.Movement.Angle);
			}
		}

		internal static void ApplyPlungerMovement(ref NativeParallelHashMap<int, PlungerState> plungerStates,
			Dictionary<int, SkinnedMeshRenderer[]> skinnedMeshRenderers)
		{
			using var enumerator = plungerStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var plungerState = ref enumerator.Current.Value;
				foreach (var skinnedMeshRenderer in skinnedMeshRenderers[enumerator.Current.Key]) {
					skinnedMeshRenderer.SetBlendShapeWeight(0, plungerState.Animation.Position);
				}
			}
		}

		internal static void ApplySpinnerMovement(ref NativeParallelHashMap<int, SpinnerState> spinnerStates,
			Dictionary<int, IRotatableAnimationComponent> rotatableComponent)
		{
			using var enumerator = spinnerStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var spinnerState = ref enumerator.Current.Value;
				var component = rotatableComponent[enumerator.Current.Key];
				component.OnRotationUpdated(spinnerState.Movement.Angle);
			}
		}

		internal static void ApplyTriggerMovement(ref NativeParallelHashMap<int, TriggerState> triggerStates,
			Dictionary<int, Transform> transforms)
		{
			using var enumerator = triggerStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var triggerState = ref enumerator.Current.Value;
				if (triggerState.AnimatedItemId == 0) {
					continue;
				}
				var triggerTransform = transforms[triggerState.AnimatedItemId];
				TriggerTransform.Update(triggerState.AnimatedItemId, in triggerState.Movement, triggerTransform);
			}
		}
	}
}
