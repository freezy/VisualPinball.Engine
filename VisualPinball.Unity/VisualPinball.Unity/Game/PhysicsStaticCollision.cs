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

			Collide(ref ball, ref state);

			// remove trial hit object pointer
			ball.CollisionEvent.ClearCollider();
		}

		private static void Collide(ref BallState ball, ref PhysicsState state)
		{
			var colliderId = ball.CollisionEvent.ColliderId;
			var collHeader = state.GetColliderHeader(colliderId);
			if (CollidesWithItem(ref collHeader, ref ball, ref state)) {
				return;
			}
			ref var cols = ref state.Colliders;
			switch (state.GetColliderType(colliderId)) {

				case ColliderType.Circle:
					ref var circleCollider = ref cols.Circle(colliderId);
					circleCollider.Collide(ref ball, in ball.CollisionEvent, ref state.Env.Random);
					break;

				case ColliderType.Plane:
					ref var planeCollider = ref cols.Plane(colliderId);
					planeCollider.Collide(ref ball, in ball.CollisionEvent, ref state.Env.Random);
					break;

				case ColliderType.Line:
					ref var lineCollider = ref cols.Line(colliderId);
					lineCollider.Collide(ref ball, ref state.EventQueue, in ball.CollisionEvent, ref state.Env.Random);
					break;

				case ColliderType.Triangle:
					ref var triangleCollider = ref cols.Triangle(colliderId);
					triangleCollider.Collide(ref ball, ref state.EventQueue, in ball.CollisionEvent, ref state.Env.Random);
					break;

				case ColliderType.Line3D:
					ref var line3DCollider = ref cols.Line3D(colliderId);
					line3DCollider.Collide(ref ball, ref state.EventQueue, in ball.CollisionEvent, ref state.Env.Random);
					break;

				case ColliderType.Point:
					ref var pointCollider = ref cols.Point(colliderId);
					pointCollider.Collide(ref ball, ref state.EventQueue, in ball.CollisionEvent, ref state.Env.Random);
					break;

				case ColliderType.Bumper:
					ref var bumperState = ref state.GetBumperState(colliderId);
					BumperCollider.Collide(ref ball, ref state.EventQueue, ref ball.CollisionEvent, ref bumperState.RingAnimation, ref bumperState.SkirtAnimation,
						in collHeader, in bumperState.Static, ref state.Env.Random);
					break;

				case ColliderType.Flipper:
					ref var flipperState = ref state.GetFlipperState(colliderId);
					ref var flipperCollider = ref cols.Flipper(colliderId);
					flipperCollider.Collide(ref ball, ref ball.CollisionEvent, ref flipperState.Movement,
						ref state.EventQueue, in ball.Id, in flipperState.Tricks, in flipperState.Static,
						in flipperState.Velocity, in flipperState.Hit, state.Env.TimeMsec
					);
					break;

				case ColliderType.Gate:
					ref var gateState = ref state.GetGateState(colliderId);
					GateCollider.Collide(ref ball, ref ball.CollisionEvent, ref gateState.Movement, ref state.EventQueue,
						in collHeader, in gateState.Static);
					break;

				case ColliderType.LineSlingShot:
					ref var surfaceState = ref state.GetSurfaceState(colliderId);
					ref var surfaceCollider = ref cols.LineSlingShot(colliderId);
					surfaceCollider.Collide(ref ball, ref state.EventQueue, in surfaceState.Slingshot,
						in ball.CollisionEvent, ref state.Env.Random);
					break;

				case ColliderType.Plunger:
					ref var plungerState = ref state.GetPlungerState(colliderId);
					PlungerCollider.Collide(ref ball, ref ball.CollisionEvent, ref plungerState.Movement, in plungerState.Static, ref state.Env.Random);
					break;

				case ColliderType.Spinner:
					ref var spinnerState = ref state.GetSpinnerState(colliderId);
					SpinnerCollider.Collide(in ball, ref ball.CollisionEvent, ref spinnerState.Movement, in spinnerState.Static);
					break;

				case ColliderType.TriggerCircle:
				case ColliderType.TriggerLine:
					TriggerCollide(ref ball, ref state, in collHeader);
					break;

				case ColliderType.KickerCircle:
					ref var kickerState = ref state.GetKickerState(colliderId);
					KickerCollider.Collide(ref ball, ref state.EventQueue, ref state.InsideOfs, ref kickerState.Collision,
						in kickerState.Static, in kickerState.CollisionMesh, in ball.CollisionEvent, collHeader.ItemId);
					break;
			}
		}

		private static bool CollidesWithItem(ref ColliderHeader collHeader, ref BallState ball, ref PhysicsState state)
		{
			ref var cols = ref state.Colliders;

			// hit target
			var colliderId = ball.CollisionEvent.ColliderId;
			if (collHeader.ItemType == ItemType.HitTarget) {

				var normal = collHeader.Type == ColliderType.Triangle
					? cols.Triangle(colliderId).Normal()
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
					ref var flipperCorrectionBlob = ref triggerState.FlipperCorrection.Value.Value;
					ref var fs = ref state.FlipperStates.GetValueByRef(flipperCorrectionBlob.FlipperItemId);
					FlipperCorrection.OnBallLeaveFlipper(ref ball, ref flipperCorrectionBlob, in fs.Movement, in fs.Tricks, in fs.Static, state.Env.TimeMsec);
				}
			}
		}
	}
}
