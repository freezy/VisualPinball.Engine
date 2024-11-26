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

using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	internal class PhysicsMovements
	{
		private readonly BumperTransform _bumperTransform = new();

		internal void ApplyBallMovement(ref PhysicsState state, Dictionary<int, Transform> transforms)
		{
			using var enumerator = state.Balls.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var ball = ref enumerator.Current.Value;
				BallMovementPhysics.Move(ball, transforms[ball.Id]);
			}
		}

		internal void ApplyFlipperMovement(ref NativeParallelHashMap<int, FlipperState> flipperStates, Dictionary<int, Transform> transforms)
		{
			using var enumerator = flipperStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var flipperState = ref enumerator.Current.Value;
				var transform = transforms[enumerator.Current.Key];

				transform.SetLocalYRotation(flipperState.Movement.Angle);
			}
		}

		internal void ApplyBumperMovement(ref NativeParallelHashMap<int, BumperState> bumperStates, Dictionary<int, Transform> transforms)
		{
			using var enumerator = bumperStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var bumperState = ref enumerator.Current.Value;
				if (bumperState.SkirtItemId != 0) {
					_bumperTransform.UpdateSkirt(in bumperState.SkirtAnimation, transforms[bumperState.SkirtItemId]);
				}
				if (bumperState.RingItemId != 0) {
					_bumperTransform.UpdateRing(bumperState.RingItemId, in bumperState.RingAnimation, transforms[bumperState.RingItemId]);
				}
			}
		}

		internal void ApplyDropTargetMovement(ref NativeParallelHashMap<int, DropTargetState> dropTargetStates, Dictionary<int, Transform> transforms)
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

		internal void ApplyHitTargetMovement(ref NativeParallelHashMap<int, HitTargetState> hitTargetStates, Dictionary<int, Transform> transforms)
		{
			using var enumerator = hitTargetStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var hitTargetState = ref enumerator.Current.Value;
				var transform = transforms[enumerator.Current.Key];
				transform.SetLocalXRotation(math.radians(hitTargetState.Animation.XRotation + hitTargetState.Static.InitialXRotation));
			}
		}

		internal void ApplyGateMovement(ref NativeParallelHashMap<int, GateState> gateStates,
			Dictionary<int, IRotatableAnimationComponent> rotatableComponent)
		{
			using var enumerator = gateStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var gateState = ref enumerator.Current.Value;
				var component = rotatableComponent[enumerator.Current.Key];
				component.OnRotationUpdated(gateState.Movement.Angle);
			}
		}

		internal void ApplyPlungerMovement(ref NativeParallelHashMap<int, PlungerState> plungerStates,
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

		internal void ApplySpinnerMovement(ref NativeParallelHashMap<int, SpinnerState> spinnerStates,
			Dictionary<int, IRotatableAnimationComponent> rotatableComponent)
		{
			using var enumerator = spinnerStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var spinnerState = ref enumerator.Current.Value;
				var component = rotatableComponent[enumerator.Current.Key];
				component.OnRotationUpdated(spinnerState.Movement.Angle);
			}
		}

		internal void ApplyTriggerMovement(ref NativeParallelHashMap<int, TriggerState> triggerStates,
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
