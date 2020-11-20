﻿// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using VisualPinball.Engine.Physics;

namespace VisualPinball.Unity
{
	internal struct Line3DCollider
	{
		private ColliderHeader _header;

		private float2 _xy;
		private float _zLow;
		private float _zHigh;
		private float3x3 _matrix;

		public static void Create(BlobBuilder builder, HitLine3D src, ref BlobPtr<Collider> dest)
		{
			ref var linePtr = ref UnsafeUtility.As<BlobPtr<Collider>, BlobPtr<Line3DCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref linePtr);
			collider.Init(src);
		}

		private void Init(HitLine3D src)
		{
			_header.Init(ColliderType.Line3D, src);

			_xy = src.Xy.ToUnityFloat2();
			_zLow = src.ZLow;
			_zHigh = src.ZHigh;
			_matrix = src.Matrix.ToUnityFloat3x3();
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
