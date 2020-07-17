using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public class Hit3DPoly : HitObject
	{
		public readonly Vertex3D[] Rgv;                                      // m_rgv
		public readonly Vertex3D Normal = new Vertex3D();                    // m_normal

		public Hit3DPoly(Vertex3D[] rgv, ItemType objType) : base(objType)
		{
			Rgv = rgv;

			// Newell's method for normal computation
			for (var i = 0; i < Rgv.Length; ++i) {
				var m = i < Rgv.Length - 1 ? i + 1 : 0;
				Normal.X += (Rgv[i].Y - Rgv[m].Y) * (Rgv[i].Z + Rgv[m].Z);
				Normal.Y += (Rgv[i].Z - Rgv[m].Z) * (Rgv[i].X + Rgv[m].X);
				Normal.Z += (Rgv[i].X - Rgv[m].X) * (Rgv[i].Y + Rgv[m].Y);
			}

			var sqrLen = Normal.X * Normal.X + Normal.Y * Normal.Y + Normal.Z * Normal.Z;
			var invLen = sqrLen > 0.0f ? -1.0f / MathF.Sqrt(sqrLen) : 0.0f; // normal NOTE is flipped! Thus we need vertices in CCW order
			Normal.X *= invLen;
			Normal.Y *= invLen;
			Normal.Z *= invLen;

			Elasticity = 0.3f;
			SetFriction(0.3f);
			Scatter = 0.0f;
		}

		public override void CalcHitBBox()
		{
			HitBBox.Left = Rgv[0].X;
			HitBBox.Right = Rgv[0].X;
			HitBBox.Top = Rgv[0].Y;
			HitBBox.Bottom = Rgv[0].Y;
			HitBBox.ZLow = Rgv[0].Z;
			HitBBox.ZHigh = Rgv[0].Z;

			for (var i = 1; i < Rgv.Length; i++) {
				HitBBox.Left = MathF.Min(Rgv[i].X, HitBBox.Left);
				HitBBox.Right = MathF.Max(Rgv[i].X, HitBBox.Right);
				HitBBox.Top = MathF.Min(Rgv[i].Y, HitBBox.Top);
				HitBBox.Bottom = MathF.Max(Rgv[i].Y, HitBBox.Bottom);
				HitBBox.ZLow = MathF.Min(Rgv[i].Z, HitBBox.ZLow);
				HitBBox.ZHigh = MathF.Max(Rgv[i].Z, HitBBox.ZHigh);
			}
		}

		public override void Collide(CollisionEvent coll, PlayerPhysics physics)
		{
			var ball = coll.Ball;
			var hitNormal = coll.HitNormal;

			/* istanbul ignore This else seems dead code to me. The actual trigger logic is handled in TriggerHitCircle and TriggerHitLine. */
			if (ObjType != ItemType.Trigger) {
				var dot = -hitNormal.Dot(ball.Hit.Vel);
				ball.Hit.Collide3DWall(Normal, Elasticity, ElasticityFalloff, Friction, Scatter);

				// manage item-specific logic
				if (Obj != null && FireEvents && dot >= Threshold) {
					Obj.OnCollision?.Invoke(this, ball, dot);
				}

			} else { // trigger (probably unused code)
				if (!ball.Hit.IsRealBall()) {
					return;
				}
				var i = ball.Hit.VpVolObjs.IndexOf(Obj);                             // if -1 then not in objects volume set (i.e not already hit)
				if (!coll.HitFlag == i < 0) { // Hit == NotAlreadyHit
					var addPos = ball.Hit.Vel.Clone().MultiplyScalar(PhysicsConstants.StaticTime);
					ball.State.Pos.Add(addPos);     // move ball slightly forward
					if (i < 0) {
						ball.Hit.VpVolObjs.Add(Obj);
						Obj.FireGroupEvent(EventId.HitEventsHit);

					} else {
						ball.Hit.VpVolObjs.RemoveAt(i);
						Obj.FireGroupEvent(EventId.HitEventsUnhit);
					}
				}
			}
		}

		public override float HitTest(Ball ball, float dTime, CollisionEvent coll, PlayerPhysics physics)
		{
			if (!IsEnabled) {
				return -1.0f;
			}

			// speed in Normal-vector direction
			var bnv = Normal.Dot(ball.Hit.Vel);

			// return if clearly ball is receding from object
			if (ObjType != ItemType.Trigger && bnv > PhysicsConstants.LowNormVel) {
				return -1.0f;
			}

			// Point on the ball that will hit the polygon, if it hits at all
			var normRadius = Normal.Clone().MultiplyScalar(ball.Data.Radius);
			var hitPos = ball.State.Pos.Clone().Sub(normRadius); // nearest point on ball ... projected radius along norm
			var planeToBall = hitPos.Clone().Sub(Rgv[0]);
			var bnd = Normal.Dot(planeToBall); // distance from plane to ball

			var bUnHit = bnv > PhysicsConstants.LowNormVel;
			var inside = bnd <= 0; // in ball inside object volume
			var rigid = ObjType != ItemType.Trigger;
			float hitTime;

			if (rigid) {
				// rigid polygon
				if (bnd < -ball.Data.Radius) {
					// (ball normal distance) excessive penetration of object skin ... no collision HACK //!! *2 necessary?
					return -1.0f;
				}

				if (bnd <= PhysicsConstants.PhysTouch) {
					if (inside || MathF.Abs(bnv) > PhysicsConstants.ContactVel // fast velocity, return zero time
					           //zero time for rigid fast bodies
					           || bnd <= -PhysicsConstants.PhysTouch) {
						// slow moving but embedded
						hitTime = 0;

					} else {
						hitTime = bnd * (float)(1.0 / (2.0 * PhysicsConstants.PhysTouch)) + 0.5f; // don't compete for fast zero time events
					}

				} else if (MathF.Abs(bnv) > PhysicsConstants.LowNormVel) {
					// not velocity low?
					hitTime = bnd / -bnv; // rate ok for safe divide

				} else {
					return -1.0f; // wait for touching
				}

			} else {
				// non-rigid polygon
				if (bnv * bnd >= 0) {
					// outside-receding || inside-approaching
					if (!ball.Hit.IsRealBall() // temporary ball
					    || MathF.Abs(bnd) >= ball.Data.Radius * 0.5 // not too close ... nor too far away
					    || inside == ball.Hit.VpVolObjs.Contains(Obj)) {
						// ...Ball outside and hit set or ball inside and no hit set
						return -1.0f;
					}

					hitTime = 0;
					bUnHit = !inside; // ball on outside is UnHit, otherwise it"s a Hit

				} else {
					hitTime = bnd / -bnv;
				}
			}

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				// time is outside this frame ... no collision
				return -1.0f;
			}

			var adv = ball.Hit.Vel.Clone().MultiplyScalar(hitTime);
			hitPos.Add(adv); // advance hit point to contact

			// Do a point in poly test, using the xy plane, to see if the hit point is inside the polygon
			// this need to be changed to a point in polygon on 3D plane
			var x2 = Rgv[0].X;
			var y2 = Rgv[0].Y;
			var hx2 = hitPos.X >= x2;
			var hy2 = hitPos.Y <= y2;
			var crossCount = 0; // count of lines which the hit point is to the left of
			for (var i = 0; i < Rgv.Length; i++) {
				var x1 = x2;
				var y1 = y2;
				var hx1 = hx2;
				var hy1 = hy2;

				var j = i < Rgv.Length - 1 ? i + 1 : 0;
				x2 = Rgv[j].X;
				y2 = Rgv[j].Y;
				hx2 = hitPos.X >= x2;
				hy2 = hitPos.Y <= y2;

				if (y1 == y2 || hy1 && hy2 || !hy1 && !hy2 || hx1 && hx2) {
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
				if (x2 - (y2 - hitPos.Y) * (x1 - x2) / (y1 - y2) > hitPos.X) {
					crossCount ^= 1;
				}
			}

			if ((crossCount & 1) != 0) {
				coll.HitNormal.Set(Normal);

				if (!rigid) {
					// non rigid body collision? return direction
					coll.HitFlag = bUnHit; // UnHit signal is receding from outside target
				}

				coll.HitDistance = bnd; // 3dhit actual contact distance ...
				//coll.M_hitRigid = rigid;                                         // collision type

				return hitTime;
			}

			return -1.0f;
		}
	}
}
