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

using Unity.Mathematics;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	internal struct SpinnerCollider : ICollider
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

		public LineCollider LineSeg0;
		public LineCollider LineSeg1;

		public ColliderBounds Bounds { get; private set; }

		public SpinnerCollider(in LineCollider lineSeg0, in LineCollider lineSeg1, ColliderInfo info) : this()
		{
			Header.Init(info, ColliderType.Spinner);
			LineSeg0 = lineSeg0;
			LineSeg1 = lineSeg1;

			Bounds = LineSeg0.Bounds;
		}

		#region Narrowphase

		public float HitTest(ref CollisionEventData collEvent, ref InsideOfs insideOfs, in BallState ball, float dTime)
		{
			var hitTime = LineCollider.HitTestBasic(ref collEvent, ref insideOfs, in LineSeg0, in ball, dTime, false, true, false); // any face, lateral, non-rigid
			if (hitTime >= 0.0f) {
				// signal the Collide() function that the hit is on the front or back side
				collEvent.HitFlag = true;
				return hitTime;
			}

			hitTime = LineCollider.HitTestBasic(ref collEvent, ref insideOfs, in LineSeg1, in ball, dTime, false, true, false); // any face, lateral, non-rigid
			if (hitTime >= 0.0f) {
				// signal the Collide() function that the hit is on the front or back side
				collEvent.HitFlag = false;
				return hitTime;
			}

			return -1.0f;
		}

		#endregion

		#region Collision

		public static void Collide(in BallState ball, ref CollisionEventData collEvent, ref SpinnerMovementState movement, in SpinnerStaticState state)
		{
			var dot = math.dot(collEvent.HitNormal, ball.Velocity);

			// hit from back doesn't count
			if (dot < 0.0f) {
				return;
			}

			var h = state.Height * 0.5f;
			// linear speed = ball speed
			// angular speed = linear/radius (height of hit)

			// h is the height of the spinner axis;
			// Since the spinner has no mass in our equation, the spot
			// h -coll.m_radius will be moving a at linear rate of
			// 'speed'. We can calculate the angular speed from that.

			movement.AngleSpeed = math.abs(dot); // use this until a better value comes along

			if (math.abs(h) > 1.0f) {
				// avoid divide by zero
				movement.AngleSpeed /= h;
			}

			movement.AngleSpeed *= state.Damping;

			// We encoded which side of the spinner the ball hit
			if (collEvent.HitFlag) {
				movement.AngleSpeed = -movement.AngleSpeed;
			}
		}

		#endregion

		#region Transformation

		public static bool IsTransformable(float4x4 matrix)
		{
			// position: fully transformable
			// scale: only uniform scale ("length")
			// rotation: only around Z axis ("rotation")

			var scale = matrix.GetScale();
			var rotation = matrix.GetRotationVector();
			var rotated = math.abs(rotation.x) > Collider.Tolerance || math.abs(rotation.y) > Collider.Tolerance;
			var uniformlyScaled = math.abs(scale.x - scale.y) < Collider.Tolerance && math.abs(scale.x - scale.z) < Collider.Tolerance && math.abs(scale.y -  scale.z) < Collider.Tolerance;

			return !rotated && uniformlyScaled;
		}

		public SpinnerCollider Transform(float4x4 matrix)
		{
			Transform(this, matrix);
			return this;
		}

		public void Transform(SpinnerCollider collider, float4x4 matrix)
		{
			#if UNITY_EDITOR
			if (!IsTransformable(matrix)) {
				throw new System.InvalidOperationException($"Matrix {matrix} cannot transform spinner.");
			}
			#endif

			LineSeg0 = collider.LineSeg0.Transform(matrix);
			LineSeg1 = collider.LineSeg1.Transform(matrix);
			Bounds = collider.LineSeg0.Bounds;
		}

		public Aabb GetTransformedAabb(float4x4 matrix)
		{
			return Bounds.Aabb.Transform(matrix);
		}

		public SpinnerCollider TransformAabb(float4x4 matrix)
		{
			Bounds = new ColliderBounds(Header.ItemId, Header.Id, GetTransformedAabb(matrix));
			return this;
		}

		#endregion

		public override string ToString() => $"SpinnerCollider[{Header.ItemId}] {LineSeg0.ToString()} | {LineSeg1.ToString()}";
	}
}
