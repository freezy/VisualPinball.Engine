// ReSharper disable CompareOfFloatsByEqualityOperator

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	public struct Poly3DCollider : ICollider, ICollidable
	{

		private ColliderHeader _header;
		private float3 _normal;
		private BlobArray _rgvBlob; // read comment at Unity.Physics.BlobArray
		public BlobArray.Accessor<float3> _rgv => new BlobArray.Accessor<float3>(ref _rgvBlob);

		public ColliderType Type => _header.Type;
		public float3 Normal() => _normal;

		public static unsafe void Create(BlobBuilder builder, Hit3DPoly src, ref BlobPtr<Collider> dest)
		{
			ref var ptr = ref UnsafeUtility.As<BlobPtr<Collider>, BlobPtr<Poly3DCollider>>(ref dest);
			var totalSize = sizeof(Poly3DCollider) + sizeof(float3) * src.Rgv.Length;
			totalSize = (totalSize + 15) & 0x7ffffff0;

			ref var collider = ref builder.Allocate(ref ptr);
			//ref var collider = ref builder.Allocate(ref ptr, totalSize, out var offsetPtr);
			//collider.Init(src, offsetPtr);
			collider.Init(src);
		}

		private unsafe void Init(Hit3DPoly src, int* offsetPtr)
		{
			_header.Init(ColliderType.Poly3D, src);
			_normal = src.Normal.ToUnityFloat3();

			var end = (byte*)offsetPtr + sizeof(Poly3DCollider);
			_rgvBlob.Offset = UnsafeEx.CalculateOffset(end, ref _rgvBlob);
			_rgvBlob.Length = src.Rgv.Length;
			for (var i = 0; i < src.Rgv.Length; i++) {
				_rgv[i] = src.Rgv[i].ToUnityFloat3();
			}
		}

		private void Init(Hit3DPoly src)
		{
			_header.Init(ColliderType.Poly3D, src);
			_normal = src.Normal.ToUnityFloat3();
		}

		public float HitTest(ref CollisionEventData collEvent, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, in BallData ball, float dTime)
		{
			// todo
			// if (!IsEnabled) {
			// 	return -1.0f;
			// }

			// speed in Normal-vector direction
			var bnv = math.dot(_normal, ball.Velocity);

			if (_header.ItemType != ItemType.Trigger && bnv > PhysicsConstants.LowNormVel) {
				// return if clearly ball is receding from object
				return -1.0f;
			}

			// Point on the ball that will hit the polygon, if it hits at all
			var hitPos = ball.Position - ball.Radius * _normal;  // nearest point on ball ... projected radius along norm

			var bnd = math.dot(_normal, hitPos - _rgv[0]); // distance from plane to ball

			var bUnHit = bnv > PhysicsConstants.LowNormVel;
			var inside = bnd <= 0f; // in ball inside object volume

			var rigid = _header.ItemType != ItemType.Trigger;
			float hitTime;

			if (rigid) {

				//rigid polygon
				if (bnd < -ball.Radius) {
					// (ball normal distance) excessive penetration of object skin ... no collision HACK
					return -1.0f;
				}

				if (bnd <= PhysicsConstants.PhysTouch) {
					if (inside
					    || math.abs(bnv) > PhysicsConstants.ContactVel         // fast velocity, return zero time
					    || bnd <= -PhysicsConstants.PhysTouch)                 // zero time for rigid fast bodies
					{
						// slow moving but embedded
						hitTime = 0f;

					} else {
						// don't compete for fast zero time events
						hitTime = bnd * (float) (1.0 / (2.0 * PhysicsConstants.PhysTouch)) + 0.5f;
					}

				} else if (math.abs(bnv) > PhysicsConstants.LowNormVel) {
					// not velocity low?
					hitTime = bnd / -bnv;                                      // rate ok for safe divide

				} else {
					return -1.0f;                                              // wait for touching
				}

			} else  { // non-rigid polygon

				// outside-receding || inside-approaching
				if (bnv * bnd >= 0f) {
					if (
						/* todo !ball.m_vpVolObjs */                           // temporary ball
						math.abs(bnd) >= ball.Radius * 0.5f                    // not too close ... nor too far away
						|| inside == BallData.IsInsideOf(in insideOfs, _header.Entity))
					{
						// ...ball outside and hit set or ball inside and no hit set
						return -1.0f;
					}

					hitTime = 0;
					bUnHit = !inside; // ball on outside is UnHit, otherwise it's a Hit

				} else {
					hitTime = bnd / -bnv;
				}
			}

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0f || hitTime > dTime) {
				// time is outside this frame ... no collision
				return -1.0f;
			}

			hitPos += hitTime * ball.Velocity; // advance hit point to contact

			// Do a point in poly test, using the xy plane, to see if the hit point is inside the polygon
			// this need to be changed to a point in polygon on 3D plane

			var x2 = _rgv[0].x;
			var y2 = _rgv[0].y;
			var hx2 = hitPos.x >= x2;
			var hy2 = hitPos.y <= y2;

			// count of lines which the hit point is to the left of
			var crossCount = 0;
			for (var i = 0; i < _rgv.Length; i++) {

				var x1 = x2;
				var y1 = y2;
				var hx1 = hx2;
				var hy1 = hy2;

				var j = i < _rgv.Length - 1 ? i + 1 : 0;
				x2 = _rgv[j].x;
				y2 = _rgv[j].y;
				hx2 = hitPos.x >= x2;
				hy2 = hitPos.y <= y2;

				if (y1 == y2
				    || hy1 && hy2
				    || !hy1 && !hy2  // if out of y range, forget about this segment
				    || hx1 && hx2)
				{
					// Hit point is on the right of the line
					continue;
				}

				if (!hx1 && !hx2) {
					crossCount ^= 1;
					continue;
				}

				if (x2 == x1) {
					if (!hx2) {
						crossCount ^= 1;
					}
					continue;
				}

				// Now the hard part - the hit point is in the line bounding box
				if (x2 - (y2 - hitPos.y) * (x1 - x2) / (y1 - y2) > hitPos.x) {
					crossCount ^= 1;
				}
			}

			if ((crossCount & 1) != 0) {
				collEvent.HitNormal = _normal;

				if (!rigid) {
					// non rigid body collision? return direction
					collEvent.HitFlag = bUnHit; // UnHit signal	is receding from outside target
				}

				collEvent.HitDistance = bnd; // 3dhit actual contact distance ...
				return hitTime;
			}

			return -1.0f;
		}

		public void Collide(ref BallData ball,  ref NativeQueue<EventData>.ParallelWriter hitEvents,
			in CollisionEventData collEvent, ref Random random)
		{
			var dot = -math.dot(collEvent.HitNormal, ball.Velocity);
			BallCollider.Collide3DWall(ref ball, in _header.Material, in collEvent, in _normal, ref random);

			if (_header.FireEvents && dot >= _header.Threshold && _header.IsPrimitive) {
				// todo m_obj->m_currentHitThreshold = dot;
				Collider.FireHitEvent(ref ball, ref hitEvents, in _header);
			}
		}
	}
}
