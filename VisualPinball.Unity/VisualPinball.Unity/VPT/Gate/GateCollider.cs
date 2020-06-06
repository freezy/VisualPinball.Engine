﻿using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Unity.Physics.Collider;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.VPT.Gate
{
	public struct GateCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		private LineCollider _lineSeg0;
		private LineCollider _lineSeg1;

		public ColliderType Type => _header.Type;

		public static void Create(BlobBuilder builder, GateHit src, ref BlobPtr<Collider> dest)
		{
			ref var ptr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<GateCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref ptr);
			collider.Init(src);
		}

		private void Init(GateHit src)
		{
			_header.Type = ColliderType.Gate;
			_header.ItemType = Collider.GetItemType(src.ObjType);
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
			// if (!this.isEnabled) {
			// 	return -1.0;
			// }

			var hitTime = LineCollider.HitTestBasic(ref collEvent, ref insideOfs, in _lineSeg1, in ball, dTime, false, true, false); // any face, lateral, non-rigid
			hitTime = LineCollider.HitTestBasic(ref collEvent, ref insideOfs, in _lineSeg0, in ball, dTime, false, true, false); // any face, lateral, non-rigid
			if (hitTime >= 0) {
				// signal the Collide() function that the hit is on the front or back side
				collEvent.HitFlag = false;
				return hitTime;
			}

			hitTime = LineCollider.HitTestBasic(ref collEvent, ref insideOfs, in _lineSeg1, in ball, dTime, false, true, false); // any face, lateral, non-rigid
			if (hitTime >= 0) {
				collEvent.HitFlag = true;
				return hitTime;
			}

			return -1.0f;
		}

		#endregion

		#region Collision

		public static void Collide(in BallData ball, ref CollisionEventData collEvent, ref GateMovementData movementData, in GateStaticData data)
		{
			var dot = math.dot(collEvent.HitNormal, ball.Velocity);
			var h = data.Height * 0.5f;

			// linear speed = ball speed
			// angular speed = linear/radius (height of hit)
			var speed = math.abs(dot);
			// h is the height of the gate axis.
			if (math.abs(h) > 1.0) {                           // avoid divide by zero
				speed /= h;
			}

			movementData.AngleSpeed = speed;
			if (!collEvent.HitFlag && !data.TwoWay) {
				movementData.AngleSpeed *= (float)(1.0 / 8.0); // Give a little bounce-back.
				return;                                        // hit from back doesn't count if not two-way
			}

			// We encoded which side of the spinner the ball hit
			if (collEvent.HitFlag && data.TwoWay) {
				movementData.AngleSpeed = -movementData.AngleSpeed;
			}

			// todo
			//this.fireHitEvent(ball);
		}

		#endregion
	}
}
