using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Unity
{
	public struct SpinnerCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		private LineCollider _lineSeg0;
		private LineCollider _lineSeg1;

		public ColliderType Type => _header.Type;

		public static void Create(BlobBuilder builder, SpinnerHit src, ref BlobPtr<Collider> dest)
		{
			ref var ptr = ref UnsafeUtility.As<BlobPtr<Collider>, BlobPtr<SpinnerCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref ptr);
			collider.Init(src);
		}

		private void Init(SpinnerHit src)
		{
			_header.Type = ColliderType.Spinner;
			_header.ItemType = src.ObjType;
			_header.Entity = new Entity {Index = src.ItemIndex, Version = src.ItemVersion};
			_header.Id = src.Id;
			_header.Material = new PhysicsMaterialData {
				Elasticity = src.Elasticity,
				ElasticityFalloff = src.ElasticityFalloff,
				Friction = src.Friction,
				Scatter = src.Scatter,
			};

			_lineSeg0 = LineCollider.Create(src.LineSeg0);
			_lineSeg1 = LineCollider.Create(src.LineSeg1);
		}

		#region Narrowphase

		public float HitTest(ref CollisionEventData collEvent, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, in BallData ball, float dTime)
		{
			// todo
			// if (!m_enabled) return -1.0f;

			var hitTime = LineCollider.HitTestBasic(ref collEvent, ref insideOfs, in _lineSeg0, in ball, dTime, false, true, false); // any face, lateral, non-rigid
			if (hitTime >= 0.0f) {
				// signal the Collide() function that the hit is on the front or back side
				collEvent.HitFlag = true;
				return hitTime;
			}

			hitTime = LineCollider.HitTestBasic(ref collEvent, ref insideOfs, in _lineSeg1, in ball, dTime, false, true, false); // any face, lateral, non-rigid
			if (hitTime >= 0.0f) {
				// signal the Collide() function that the hit is on the front or back side
				collEvent.HitFlag = false;
				return hitTime;
			}

			return -1.0f;
		}

		#endregion

		#region Collision

		public static void Collide(in BallData ball, ref CollisionEventData collEvent, ref SpinnerMovementData movementData, in SpinnerStaticData data)
		{
			var dot = math.dot(collEvent.HitNormal, ball.Velocity);

			// hit from back doesn't count
			if (dot < 0.0f) {
				return;
			}

			var h = data.Height * 0.5f;
			// linear speed = ball speed
			// angular speed = linear/radius (height of hit)

			// h is the height of the spinner axis;
			// Since the spinner has no mass in our equation, the spot
			// h -coll.m_radius will be moving a at linear rate of
			// 'speed'. We can calculate the angular speed from that.

			movementData.AngleSpeed = math.abs(dot); // use this until a better value comes along

			if (math.abs(h) > 1.0f) {
				// avoid divide by zero
				movementData.AngleSpeed /= h;
			}

			movementData.AngleSpeed *= data.Damping;

			// We encoded which side of the spinner the ball hit
			if (collEvent.HitFlag) {
				movementData.AngleSpeed = -movementData.AngleSpeed;
			}
		}

		#endregion
	}
}
