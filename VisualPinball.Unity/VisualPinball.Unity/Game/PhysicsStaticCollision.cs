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

// ReSharper disable ConvertIfStatementToSwitchStatement

using Unity.Mathematics;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	internal static class PhysicsStaticCollision
	{
		internal static void Collide(float hitTime, ref BallState ball, ref PhysicsState state)
		{
			// find balls with hit objects and minimum time
			if (ball.CollisionEvent.ColliderId < 0 || ball.CollisionEvent.HitTime > hitTime) {
				return;
			}

			if (ball.CollisionEvent.IsKinematic) {
				Collide(ref state.KinematicColliders, ref ball, ref state);
			} else {
				Collide(ref state.Colliders, ref ball, ref state);
			}
		}

		private static void Collide(ref NativeColliders colliders, ref BallState ball, ref PhysicsState state)
		{
			var colliderId = ball.CollisionEvent.ColliderId;
			var collHeader = state.GetColliderHeader(ref colliders, colliderId);
			if (CollidesWithItem(ref colliders, ref collHeader, ref ball, ref state)) {
				return;
			}
			switch (state.GetColliderType(ref colliders, colliderId)) {

				case ColliderType.Circle:
					ref var circleCollider = ref colliders.Circle(colliderId);
					circleCollider.Collide(ref ball, in ball.CollisionEvent, ref state.Env.Random);
					break;

				case ColliderType.Plane:
					ref var planeCollider = ref colliders.Plane(colliderId);
					planeCollider.Collide(ref ball, in ball.CollisionEvent, ref state.Env.Random);
					break;

				case ColliderType.Line:
					ref var lineCollider = ref colliders.Line(colliderId);
					lineCollider.Collide(ref ball, ref state.EventQueue, in ball.CollisionEvent, ref state.Env.Random);
					break;

				case ColliderType.LineZ:
					ref var lineZCollider = ref colliders.LineZ(colliderId);
					lineZCollider.Collide(ref ball, ref state.EventQueue, in ball.CollisionEvent, ref state.Env.Random);
					break;

				case ColliderType.Triangle:
					ref var triangleCollider = ref colliders.Triangle(colliderId);
					triangleCollider.Collide(ref ball, ref state.EventQueue, in ball.CollisionEvent, ref state.Env.Random);
					break;

				case ColliderType.Line3D:
					ref var line3DCollider = ref colliders.Line3D(colliderId);
					line3DCollider.Collide(ref ball, ref state.EventQueue, in ball.CollisionEvent, ref state.Env.Random);
					break;

				case ColliderType.Point:
					ref var pointCollider = ref colliders.Point(colliderId);
					pointCollider.Collide(ref ball, ref state.EventQueue, in ball.CollisionEvent, ref state.Env.Random);
					break;

				case ColliderType.Bumper:
					ref var bumperState = ref state.GetBumperState(colliderId, ref colliders);
					BumperCollider.Collide(ref ball, ref state.EventQueue, ref ball.CollisionEvent, ref bumperState.RingAnimation, ref bumperState.SkirtAnimation,
						in collHeader, in bumperState.Static, ref state.Env.Random, ref state.InsideOfs);
					break;

				case ColliderType.Flipper:
					ref var flipperState = ref state.GetFlipperState(colliderId);
					ref var flipperCollider = ref colliders.Flipper(colliderId);
					ref var matrix = ref state.GetNonTransformableColliderMatrix(colliderId);
					var matrixInv = math.inverse(matrix);

					var ballTransformed = ball;
					ballTransformed.Transform(matrixInv);

					var colEventTransformed = ball.CollisionEvent;
					colEventTransformed.Transform(matrixInv);

					flipperCollider.Collide(ref ballTransformed, ref colEventTransformed, ref flipperState.Movement,
						ref state.EventQueue, in ball.Id, in flipperState.Tricks, in flipperState.Static,
						in flipperState.Velocity, in flipperState.Hit, state.Env.TimeMsec
					);

					colEventTransformed.Transform(matrix);
					ballTransformed.Transform(matrix);
					ball = ballTransformed;
					ball.CollisionEvent = colEventTransformed;

					break;

				case ColliderType.Gate:
					ref var gateState = ref state.GetGateState(colliderId, ref colliders);
					GateCollider.Collide(ref ball, ref ball.CollisionEvent, ref gateState.Movement, ref state.EventQueue,
						in collHeader, in gateState.Static);
					break;

				case ColliderType.LineSlingShot:
					ref var surfaceState = ref state.GetSurfaceState(colliderId);
					ref var surfaceCollider = ref colliders.LineSlingShot(colliderId);
					surfaceCollider.Collide(ref ball, ref state.EventQueue, in surfaceState.Slingshot,
						in ball.CollisionEvent, ref state.Env.Random);
					break;

				case ColliderType.Plunger:
					ref var plungerState = ref state.GetPlungerState(colliderId);
					PlungerCollider.Collide(ref ball, ref ball.CollisionEvent, ref plungerState.Movement, in plungerState.Static, ref state.Env.Random);
					break;

				case ColliderType.Spinner:
					ref var spinnerState = ref state.GetSpinnerState(colliderId, ref colliders);
					SpinnerCollider.Collide(in ball, ref ball.CollisionEvent, ref spinnerState.Movement, in spinnerState.Static);
					break;

				case ColliderType.TriggerCircle:
				case ColliderType.TriggerLine:
					TriggerCollide(ref ball, ref state, in collHeader);
					break;

				case ColliderType.KickerCircle:
					ref var kickerState = ref state.GetKickerState(colliderId);
					KickerCollider.Collide(ref ball, ref state.EventQueue, ref state.InsideOfs, ref kickerState.Collision,
						in kickerState.Static, in kickerState.CollisionMesh, in ball.CollisionEvent, collHeader.ItemId, false);
					break;
			}

			// remove trial hit object pointer
			ball.CollisionEvent.ClearCollider();
		}

		private static bool CollidesWithItem(ref NativeColliders colliders, ref ColliderHeader collHeader, ref BallState ball, ref PhysicsState state)
		{
			// hit target
			var colliderId = ball.CollisionEvent.ColliderId;
			if (collHeader.ItemType == ItemType.HitTarget) {

				var normal = collHeader.Type == ColliderType.Triangle
					? colliders.Triangle(colliderId).Normal()
					: ball.CollisionEvent.HitNormal;

				if (state.HasDropTargetState(colliderId)) {
					ref var dropTargetState = ref state.GetDropTargetState(colliderId);
					TargetCollider.DropTargetCollide(ref ball, ref state.EventQueue, ref dropTargetState.Animation, in normal, in ball.CollisionEvent, in collHeader, ref state.Env.Random);
					return true;
				}

				if (state.HasHitTargetState(colliderId)) {
					ref var hitTargetState = ref state.GetHitTargetState(colliderId);
					TargetCollider.HitTargetCollide(ref ball, ref state.EventQueue, ref hitTargetState.Animation, in normal, in ball.CollisionEvent, in collHeader, ref state.Env.Random);
					return true;
				}

			// trigger
			} else if (collHeader.ItemType == ItemType.Trigger) {
				TriggerCollide(ref ball, ref state, in collHeader);
				return true;
			}

			return false;
		}

		private static void TriggerCollide(ref BallState ball, ref PhysicsState state, in ColliderHeader collHeader)
		{
			ref var triggerState = ref state.GetTriggerState(collHeader.Id);
			TriggerCollider.Collide(ref ball, ref state.EventQueue, ref ball.CollisionEvent, ref state.InsideOfs, ref triggerState.Animation, in collHeader);

			if (triggerState.FlipperCorrection.IsEnabled) {
				if (triggerState.Animation.UnHitEvent) {
					ref var flipperCorrectionState = ref triggerState.FlipperCorrection;
					ref var fs = ref state.FlipperStates.GetValueByRef(flipperCorrectionState.FlipperItemId);
					FlipperCorrection.OnBallLeaveFlipper(ref ball, ref flipperCorrectionState, in fs.Movement, in fs.Tricks, in fs.Static, state.Env.TimeMsec);
				}
			}
		}
	}
}
