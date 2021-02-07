// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using Unity.Profiling;

namespace VisualPinball.Unity
{
	internal struct Line3DCollider : ICollider
	{
		public int Id => _header.Id;

		private ColliderHeader _header;

		// these are all used when casting this to LineZCollider,
		// so the order is important too.
		// ReSharper disable once NotAccessedField.Local
		private readonly float2 _xy;
		// ReSharper disable once NotAccessedField.Local
		private readonly float _zLow;
		// ReSharper disable once NotAccessedField.Local
		private readonly float _zHigh;
		private readonly float3x3 _matrix;

		public readonly ColliderBounds Bounds;

		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("Line3DCollider.Allocate");

		public Line3DCollider(float3 v1, float3 v2, ColliderInfo info) : this()
		{
			_header.Init(info, ColliderType.Line3D);

			var vLine = math.normalize(v2 - v1);

			// Axis of rotation to make 3D cylinder a cylinder along the z-axis
			var transAxis = new float3(vLine.y, -vLine.x, 0.0f);

			var l = math.lengthsq(transAxis);

			// line already points in z axis?
			if (l <= 1e-6f) {
				// choose arbitrary rotation vector
				transAxis.Set(1, 0f, 0f);
			} else {
				transAxis /= math.sqrt(l);
			}

			// Angle to rotate the line into the z-axis
			var dot = vLine.z;

			_matrix = new float3x3();
			_matrix.RotationAroundAxis(transAxis, -math.sqrt(1 - dot * dot), dot);

			var trans1 = math.mul(_matrix, v1);
			var trans2Z = math.mul(_matrix, v2).z;

			// set up HitLineZ parameters
			_xy.x = trans1.x;
			_xy.y = trans1.y;
			_zLow = math.min(trans1.z, trans2Z);
			_zHigh = math.max(trans1.z, trans2Z);

			Bounds = new ColliderBounds(_header.Entity, _header.Id, new Aabb(
				math.min(v1.x, v2.x),
				math.max(v1.x, v2.x),
				math.min(v1.y, v2.y),
				math.max(v1.y, v2.y),
				math.min(v1.z, v2.z),
				math.max(v1.z, v2.z)
			));
		}

		public unsafe void Allocate(BlobBuilder builder, ref BlobBuilderArray<BlobPtr<Collider>> colliders)
		{
			PerfMarker.Begin();
			ref var ptr = ref UnsafeUtility.As<BlobPtr<Collider>, BlobPtr<Line3DCollider>>(ref colliders[_header.Id]);
			ref var collider = ref builder.Allocate(ref ptr);
			UnsafeUtility.MemCpy(
				UnsafeUtility.AddressOf(ref collider),
				UnsafeUtility.AddressOf(ref this),
				sizeof(Line3DCollider)
			);
			PerfMarker.End();
		}

		#region Narrowphase

		public float HitTest(ref CollisionEventData collEvent, in BallData ball, float dTime)
		{
			return HitTest(ref collEvent, ref this, in ball, dTime);
		}

		private static float HitTest(ref CollisionEventData collEvent, ref Line3DCollider coll, in BallData ball, float dTime)
		{
			// todo
			// if (!IsEnabled) {
			// 	return -1.0f;
			// }

			var hitTestBall = ball;

			// transform ball to cylinder coordinate system
			hitTestBall.Position = math.mul(coll._matrix, ball.Position);
			hitTestBall.Velocity = math.mul(coll._matrix, ball.Velocity);

			ref var lineZColl = ref UnsafeUtility.As<Line3DCollider, LineZCollider>(ref coll);
			var hitTime = LineZCollider.HitTest(ref collEvent, in lineZColl, in hitTestBall, dTime);

			// transform hit normal back to world coordinate system
			if (hitTime >= 0) {
				collEvent.HitNormal = math.mul(coll._matrix, collEvent.HitNormal);
			}

			return hitTime;
		}

		#endregion

		public void Collide(ref BallData ball, ref NativeQueue<EventData>.ParallelWriter hitEvents,
			in Entity ballEntity, in CollisionEventData collEvent, ref Random random)
		{
			var dot = math.dot(collEvent.HitNormal, ball.Velocity);
			BallCollider.Collide3DWall(ref ball, in _header.Material, in collEvent, in collEvent.HitNormal, ref random);

			if (_header.FireEvents && dot >= _header.Threshold && _header.IsPrimitive) {
				// todo m_obj->m_currentHitThreshold = dot;
				Collider.FireHitEvent(ref ball, ref hitEvents, in ballEntity, in _header);
			}
		}
	}
}
