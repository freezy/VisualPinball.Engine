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
		internal void ApplyBallMovement(ref PhysicsState state, Dictionary<int, Transform> transforms)
		{
			using var enumerator = state.Balls.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var ball = ref enumerator.Current.Value;
				if (ball.IsFrozen) {
					continue;
				}
				BallMovementPhysics.Move(ball, transforms[ball.Id]);
			}
		}

		internal void ApplyFlipperMovement(ref NativeParallelHashMap<int, FlipperState> flipperStates,
			Dictionary<int, IAnimationValueEmitter<float>> floatAnimatedComponent)
		{
			using var enumerator = flipperStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var flipperState = ref enumerator.Current.Value;
				var emitter = floatAnimatedComponent[enumerator.Current.Key];
				emitter.UpdateAnimationValue(flipperState.Movement.Angle);
			}
		}

		internal void ApplyBumperMovement(
			ref NativeParallelHashMap<int, BumperState> bumperStates,
			Dictionary<int, IAnimationValueEmitter<float>> floatAnimatedComponent,
			Dictionary<int, IAnimationValueEmitter<float2>> float2AnimatedComponent
		) {
			using var enumerator = bumperStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var bumperState = ref enumerator.Current.Value;

				if (bumperState.RingItemId != 0) {
					var ringEmitter = floatAnimatedComponent[enumerator.Current.Key];
					ringEmitter.UpdateAnimationValue(bumperState.RingAnimation.Offset);
				}

				if (bumperState.SkirtItemId != 0) {
					var skirtEmitter = float2AnimatedComponent[enumerator.Current.Key];
					skirtEmitter.UpdateAnimationValue(bumperState.SkirtAnimation.Rotation);
				}
			}
		}

		internal void ApplyDropTargetMovement(ref NativeParallelHashMap<int, DropTargetState> dropTargetStates,
			Dictionary<int, IAnimationValueEmitter<float>> floatAnimatedComponent)
		{
			using var enumerator = dropTargetStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var dropTargetState = ref enumerator.Current.Value;
				if (dropTargetState.AnimatedItemId == 0) { // 0 means no animation component
					continue;
				}

				var emitter = floatAnimatedComponent[enumerator.Current.Key];
				emitter.UpdateAnimationValue(dropTargetState.Animation.ZOffset);
			}
		}

		internal void ApplyHitTargetMovement(ref NativeParallelHashMap<int, HitTargetState> hitTargetStates,
			Dictionary<int, IAnimationValueEmitter<float>> floatAnimatedComponent)
		{
			using var enumerator = hitTargetStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var hitTargetState = ref enumerator.Current.Value;
				if (hitTargetState.AnimatedItemId == 0) {
					continue;
				}

				var emitter = floatAnimatedComponent[enumerator.Current.Key];
				emitter.UpdateAnimationValue(hitTargetState.Animation.XRotation);
			}
		}

		internal void ApplyGateMovement(ref NativeParallelHashMap<int, GateState> gateStates,
			Dictionary<int, IAnimationValueEmitter<float>> floatAnimatedComponent)
		{
			using var enumerator = gateStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var gateState = ref enumerator.Current.Value;
				var component = floatAnimatedComponent[enumerator.Current.Key];
				component.UpdateAnimationValue(gateState.Movement.Angle);
			}
		}

		internal void ApplyPlungerMovement(ref NativeParallelHashMap<int, PlungerState> plungerStates,
			Dictionary<int, IAnimationValueEmitter<float>> floatAnimatedComponent)
		{
			using var enumerator = plungerStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var plungerState = ref enumerator.Current.Value;
				var component = floatAnimatedComponent[enumerator.Current.Key];
				component.UpdateAnimationValue(plungerState.Animation.Position);
			}
		}

		internal void ApplySpinnerMovement(ref NativeParallelHashMap<int, SpinnerState> spinnerStates,
			Dictionary<int, IAnimationValueEmitter<float>> floatAnimatedComponent)
		{
			using var enumerator = spinnerStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var spinnerState = ref enumerator.Current.Value;
				var component = floatAnimatedComponent[enumerator.Current.Key];
				component.UpdateAnimationValue(spinnerState.Movement.Angle);
			}
		}

		internal void ApplyTriggerMovement(ref NativeParallelHashMap<int, TriggerState> triggerStates,
			Dictionary<int, IAnimationValueEmitter<float>> floatAnimatedComponent)
		{
			using var enumerator = triggerStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				ref var triggerState = ref enumerator.Current.Value;
				if (triggerState.AnimatedItemId == 0) {
					continue;
				}
				var component = floatAnimatedComponent[enumerator.Current.Key];
				component.UpdateAnimationValue(triggerState.Movement.HeightOffset);
			}
		}
	}
}
