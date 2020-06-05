﻿using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collider
{
	public struct LineSlingshotCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		private float2 _v1;
		private float2 _v2;
		private float2 _normal;
		private float _length;
		private float _zLow;
		private float _zHigh;
		private float _force;

		public ColliderType Type => _header.Type;

		public static void Create(BlobBuilder builder, LineSegSlingshot src, ref BlobPtr<Collider> dest)
		{
			ref var linePtr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<LineSlingshotCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref linePtr);
			collider.Init(src);
		}

		private void Init(LineSegSlingshot src)
		{
			_header.Init(ColliderType.LineSlingShot, src);

			_v1 = src.V1.ToUnityFloat2();
			_v2 = src.V2.ToUnityFloat2();
			_normal = src.Normal.ToUnityFloat2();
			_length = src.Length;
			_zHigh = src.HitBBox.ZHigh;
			_zLow = src.HitBBox.ZLow;
			_force = src.Force;
		}

		public float HitTest(ref CollisionEventData collEvent, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, in BallData ball, float dTime)
		{
			return HitTest(ref collEvent, ref this, ref insideOfs, in ball, dTime);
		}

		private static float HitTest(ref CollisionEventData collEvent, ref LineSlingshotCollider coll, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, in BallData ball, float dTime)
		{
			ref var lineColl = ref UnsafeUtilityEx.As<LineSlingshotCollider, LineCollider>(ref coll);
			return LineCollider.HitTestBasic(ref collEvent, ref insideOfs, in lineColl, in ball, dTime, true, true, true);
		}

		public void Collide(ref BallData ball, in LineSlingshotData slingshotData, in CollisionEventData collEvent, ref Random random)
		{
			var hitNormal = collEvent.HitNormal;

			// normal velocity to slingshot
			var dot = math.dot(collEvent.HitNormal, ball.Velocity);

			// normal greater than threshold?
			var threshold = dot <= -slingshotData.Threshold;

			if (!slingshotData.IsDisabled && threshold) { // enabled and if velocity greater than threshold level

				// length of segment, Unit TAN points from V1 to V2
				var len = (_v2.x - _v1.x) * hitNormal.y - (_v2.y - _v1.y) * hitNormal.x;

				// project ball radius along norm
				var hitPoint = new float2(ball.Position.x - hitNormal.x * ball.Radius, ball.Position.y - hitNormal.y * ball.Radius);

				// hitPoint will now be the point where the ball hits the line
				// Calculate this distance from the center of the slingshot to get force

				// distance to hit from V1
				var btd = (hitPoint.x - _v1.x) * hitNormal.y - (hitPoint.y - _v1.y) * hitNormal.x;
				var force = math.abs(len) > 1.0e-6f ? (btd + btd) / len - 1.0f : -1.0f; // -1..+1

				//!! maximum value 0.5 ...I think this should have been 1.0...oh well
				force = 0.5f * (1.0f - force * force);

				// will match the previous physics
				force *= _force; //-80;

				// boost velocity, drive into slingshot (counter normal), allow CollideWall to handle the remainder
				ball.Velocity -= hitNormal * force;
			}

			BallCollider.Collide3DWall(ref ball, in _header.Material, in collEvent, in hitNormal, ref random);

			// todo event
			// if (m_obj && m_fe && !m_psurface->m_disabled && threshold) {
			// 	// is this the same place as last event? if same then ignore it
			// 	const float dist_ls = (pball->m_lastEventPos - pball->m_d.m_pos).LengthSquared();
			// 	pball->m_lastEventPos = pball->m_d.m_pos; //remember last collide position
			//
			// 	if (dist_ls > 0.25f) //!! magic distance, must be a new place if only by a little
			// 	{
			// 		((IFireEvents*) m_obj)->FireGroupEvent(DISPID_SurfaceEvents_Slingshot);
			// 		m_slingshotanim.m_TimeReset = g_pplayer->m_time_msec + 100;
			// 	}
			// }
		}
	}
}
