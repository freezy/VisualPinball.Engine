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
using Unity.Mathematics;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Like <see cref="LineCollider">Line</see>, but with different collision code.
	/// </summary>
	///
	/// <remarks>
	/// Defined by two points (float2), zHigh, zLow
	/// </remarks>
	internal struct LineSlingshotCollider : ICollider
	{
		public int Id
		{
			get => Header.Id;
			set => Header.Id = value;
		}

		public bool IsFullyTransformable => false;

		public ColliderHeader Header;

		public float2 V1;
		public float2 V2;

		public float2 Normal;
		public float ZLow;
		public float ZHigh;
		private float _length;

		private readonly float _force;

		public ColliderBounds Bounds { get; private set; }

		public LineSlingshotCollider(float force, float2 v1, float2 v2, float zLow, float zHigh, ColliderInfo info) : this()
		{
			Header.Init(info, ColliderType.LineSlingShot);
			_force = force;
			V1 = v1;
			V2 = v2;
			ZLow = zLow;
			ZHigh = zHigh;
			CalcNormal();
			CalculateBounds();
		}

		private void CalcNormal()
		{
			var vT = new float2(V1.x - V2.x, V1.y - V2.y);
			_length = math.length(vT);

			// Set up line normal
			var invLength = 1.0f / _length;
			Normal.x = vT.y * invLength;
			Normal.y = -vT.x * invLength;
		}

		#region Narrowphase

		public float HitTest(ref CollisionEventData collEvent, ref InsideOfs insideOfs, in BallState ball, float dTime)
		{
			return HitTest(ref collEvent, ref this, ref insideOfs, in ball, dTime);
		}

		private static float HitTest(ref CollisionEventData collEvent, ref LineSlingshotCollider coll, ref InsideOfs insideOfs, in BallState ball, float dTime)
		{
			ref var lineColl = ref UnsafeUtility.As<LineSlingshotCollider, LineCollider>(ref coll);
			return LineCollider.HitTestBasic(ref collEvent, ref insideOfs, in lineColl, in ball, dTime, true, true, true);
		}

		#endregion

		#region Collision

		public void Collide(ref BallState ball, ref NativeQueue<EventData>.ParallelWriter events, in LineSlingshotState slingshotState, in CollisionEventData collEvent, ref Random random)
		{
			var hitNormal = collEvent.HitNormal;

			// normal velocity to slingshot
			var dot = math.dot(collEvent.HitNormal, ball.Velocity);

			// normal greater than threshold?
			var threshold = dot <= -slingshotState.Threshold;

			if (!slingshotState.IsDisabled && threshold) { // enabled and if velocity greater than threshold level

				// length of segment, Unit TAN points from V1 to V2
				var len = (V2.x - V1.x) * hitNormal.y - (V2.y - V1.y) * hitNormal.x;

				// project ball radius along norm
				var hitPoint = new float2(ball.Position.x - hitNormal.x * ball.Radius, ball.Position.y - hitNormal.y * ball.Radius);

				// hitPoint will now be the point where the ball hits the line
				// Calculate this distance from the center of the slingshot to get force

				// distance to hit from V1
				var btd = (hitPoint.x - V1.x) * hitNormal.y - (hitPoint.y - V1.y) * hitNormal.x;
				var force = math.abs(len) > 1.0e-6f ? (btd + btd) / len - 1.0f : -1.0f; // -1..+1

				//!! maximum value 0.5 ...I think this should have been 1.0...oh well
				force = 0.5f * (1.0f - force * force);

				// will match the previous physics
				force *= _force; //-80;

				// boost velocity, drive into slingshot (counter normal), allow CollideWall to handle the remainder
				ball.Velocity -= hitNormal * force;
			}

			BallCollider.Collide3DWall(ref ball, in Header.Material, in collEvent, in hitNormal, ref random);

			if (/*m_obj &&*/ Header.FireEvents /*&& !m_psurface->m_disabled*/ && threshold) { // todo enabled

				// is this the same place as last event? if same then ignore it
				var distLs = math.lengthsq(ball.EventPosition - ball.Position);
				ball.EventPosition = ball.Position; // remember last collide position

				// !! magic distance, must be a new place if only by a little
				if (distLs > 0.25f) {
					events.Enqueue(new EventData(EventId.SurfaceEventsSlingshot, Header.ItemId, ball.Id, true));

					// todo slingshot animation
					// m_slingshotanim.m_TimeReset = g_pplayer->m_time_msec + 100;
				}
			}
		}

		#endregion

		#region Transformation

		public static bool IsTransformable(float4x4 matrix)
		{
			// position: fully transformable: 3d (center + ZLow)
			// scale: fully scalable
			// rotation: can be z-rotated, x/y rotation is not supported.

			var rotation = matrix.GetRotationVector();
			var xyRotated = math.abs(rotation.x) > Collider.Tolerance || math.abs(rotation.y) > Collider.Tolerance;

			return !xyRotated;
		}

		public LineSlingshotCollider Transform(float4x4 matrix)
		{
			Transform(this, matrix);
			return this;
		}

		public void Transform(LineSlingshotCollider lineCollider, float4x4 matrix)
		{
			#if UNITY_EDITOR
			if (!IsTransformable(matrix)) {
				throw new System.InvalidOperationException($"Matrix {matrix} cannot transform slingshot collider.");
			}
			#endif

			var s = matrix.GetScale();
			var t = matrix.GetTranslation();
			V1 = matrix.MultiplyPoint(new float3(lineCollider.V1, 0)).xy;
			V2 = matrix.MultiplyPoint(new float3(lineCollider.V2, 0)).xy;
			ZHigh = t.z + lineCollider.ZHigh * s.z;
			ZLow = t.z + lineCollider.ZLow * s.z;

			CalcNormal();
			CalculateBounds();
		}

		public Aabb GetTransformedAabb(float4x4 matrix)
		{
			var p1 = matrix.MultiplyPoint(new float3(V1, ZLow));
			var p2 = matrix.MultiplyPoint(new float3(V1, ZHigh));
			var p3 = matrix.MultiplyPoint(new float3(V2, ZLow));
			var p4 = matrix.MultiplyPoint(new float3(V2, ZHigh));

			var min = math.min(p1, math.min(p2, math.min(p3, p4)));
			var max = math.max(p1, math.max(p2, math.max(p3, p4)));

			return new Aabb(min, max);
		}

		public LineSlingshotCollider TransformAabb(float4x4 matrix)
		{
			Bounds = new ColliderBounds(Header.ItemId, Header.Id, GetTransformedAabb(matrix));
			return this;
		}

		#endregion

		private void CalculateBounds()
		{
			// TransformAabb() takes in a matrix, this is faster if the matrix has been applied to the collider already.
			Bounds = new ColliderBounds(Header.ItemId, Header.Id, new Aabb(
				math.min(V1.x, V2.x),
				math.max(V1.x, V2.x),
				math.min(V1.y, V2.y),
				math.max(V1.y, V2.y),
				ZLow,
				ZHigh
			));
		}

		public override string ToString() => $"LineSlingshotCollider[{Header.ItemId}] ({V1.x}/{V1.y}@{ZLow}) -> ({V2.x}/{V2.y}@{ZHigh}) at ({Normal.x}/{Normal.y}), len: {_length}";
	}
}
