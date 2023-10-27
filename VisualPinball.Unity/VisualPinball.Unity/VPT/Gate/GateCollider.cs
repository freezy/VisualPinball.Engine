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

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	internal struct GateCollider : ICollider
	{
		public int Id
		{
			get => Header.Id;
			set {
				Header.Id = value;
				var bounds = Bounds;
				bounds.ColliderId = value;
				Bounds = bounds;
			}
		}

		public ColliderHeader Header;

		public readonly LineCollider LineSeg0;
		public readonly LineCollider LineSeg1;

		public ColliderBounds Bounds { get; private set; }

		public GateCollider(in LineCollider lineSeg0, in LineCollider lineSeg1, ColliderInfo info) : this()
		{
			Header.Init(info, ColliderType.Gate);
			LineSeg0 = lineSeg0;
			LineSeg1 = lineSeg1;

			Bounds = LineSeg0.Bounds;
		}

		public unsafe void Allocate(BlobBuilder builder, ref BlobBuilderArray<BlobPtr<Collider>> colliders)
		{
			ref var ptr = ref UnsafeUtility.As<BlobPtr<Collider>, BlobPtr<GateCollider>>(ref colliders[Header.Id]);
			ref var collider = ref builder.Allocate(ref ptr);
			UnsafeUtility.MemCpy(
				UnsafeUtility.AddressOf(ref collider),
				UnsafeUtility.AddressOf(ref this),
				sizeof(GateCollider)
			);
		}

		#region Narrowphase

		public float HitTest(ref CollisionEventData collEvent, ref InsideOfs insideOfs, in BallState ball, float dTime)
		{
			// todo
			// if (!this.isEnabled) {
			// 	return -1.0;
			// }

			var hitTime = LineCollider.HitTestBasic(ref collEvent, ref insideOfs, in LineSeg0, in ball, dTime, false, true, false); // any face, lateral, non-rigid
			if (hitTime >= 0) {
				// signal the Collide() function that the hit is on the front or back side
				collEvent.HitFlag = false;
				return hitTime;
			}

			hitTime = LineCollider.HitTestBasic(ref collEvent, ref insideOfs, in LineSeg1, in ball, dTime, false, true, false); // any face, lateral, non-rigid
			if (hitTime >= 0) {
				collEvent.HitFlag = true;
				return hitTime;
			}

			return -1.0f;
		}

		#endregion

		#region Collision

		public static void Collide(ref BallState ball, ref CollisionEventData collEvent, ref GateMovementState movementState,
			ref NativeQueue<EventData>.ParallelWriter events, in ColliderHeader collHeader, in GateStaticState state)
		{
			var dot = math.dot(collEvent.HitNormal, ball.Velocity);
			var h = state.Height * 0.5f;

			// linear speed = ball speed
			// angular speed = linear/radius (height of hit)
			var speed = -math.abs(dot);
			// h is the height of the gate axis.
			if (math.abs(h) > 1.0) {                           // avoid divide by zero
				speed /= h;
			}

			movementState.AngleSpeed = speed;
			if (!collEvent.HitFlag && !state.TwoWay) {
				movementState.HitDirection = dot > 0;
				movementState.AngleSpeed *= (float)(1.0 / 8.0); // Give a little bounce-back.
				return;                                        // hit from back doesn't count if not two-way
			}

			movementState.HitDirection = false;

			// We encoded which side of the spinner the ball hit
			if (collEvent.HitFlag && state.TwoWay) {

				movementState.AngleSpeed = -movementState.AngleSpeed;
			}

			Collider.FireHitEvent(ref ball, ref events, in collHeader);
		}

		#endregion
	}
}
