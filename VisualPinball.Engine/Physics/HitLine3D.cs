using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public class HitLine3D : HitLineZ
	{
		private readonly Matrix2D _matrix = new Matrix2D();
		private readonly float _zLow;
		private readonly float _zHigh;

		public HitLine3D(Vertex3D v1, Vertex3D v2) : base(new Vertex2D())
		{
			var vLine = v2.Clone().Sub(v1);
			vLine.Normalize();

			// Axis of rotation to make 3D cylinder a cylinder along the z-axis
			var transAxis = new Vertex3D(vLine.Y, -vLine.X, 0);
			var l = transAxis.LengthSq();
			if (l <= 1e-6) {
				// line already points in z axis?
				transAxis.Set(1, 0, 0); // choose arbitrary rotation vector

			} else {
				transAxis.DivideScalar(MathF.Sqrt(l));
			}

			// Angle to rotate the line into the z-axis
			var dot = vLine.Z; //vLine.Dot(&vup);

			_matrix.RotationAroundAxis(transAxis, -MathF.Sqrt(1 - dot * dot), dot);

			var vTrans1 = v1.Clone().ApplyMatrix2D(_matrix);
			var vTrans2 = v2.Clone().ApplyMatrix2D(_matrix);
			var vTrans2Z = vTrans2.Z;

			// set up HitLineZ parameters
			Xy.Set(vTrans1.X, vTrans1.Y);
			_zLow = MathF.Min(vTrans1.Z, vTrans2Z);
			_zHigh = MathF.Max(vTrans1.Z, vTrans2Z);

			HitBBox.Left = MathF.Min(v1.X, v2.X);
			HitBBox.Right = MathF.Max(v1.X, v2.X);
			HitBBox.Top = MathF.Min(v1.Y, v2.Y);
			HitBBox.Bottom = MathF.Max(v1.Y, v2.Y);
			HitBBox.ZLow = MathF.Min(v1.Z, v2.Z);
			HitBBox.ZHigh = MathF.Max(v1.Z, v2.Z);
		}

		public override void CalcHitBBox() {
			// already one in constructor
		}

		public override float HitTest(Ball ball, float dTime, CollisionEvent coll, PlayerPhysics physics) {
			if (!IsEnabled) {
				return -1.0f;
			}

			// transform ball to cylinder coordinate system
			var oldPos = ball.State.Pos.Clone();
			var oldVel = ball.Hit.Vel.Clone();
			ball.State.Pos.ApplyMatrix2D(_matrix);
			ball.State.Pos.ApplyMatrix2D(_matrix);

			// and update z bounds of LineZ with transformed coordinates
			var oldZ = new Vertex2D(HitBBox.ZLow, HitBBox.ZHigh);
			HitBBox.ZLow = _zLow; // HACK; needed below // evil cast to non-const, should actually change the stupid HitLineZ to have explicit z coordinates!
			HitBBox.ZHigh = _zHigh; // dto.

			var hitTime = base.HitTest(ball, dTime, coll, physics);

			ball.State.Pos.Set(oldPos.X, oldPos.Y, oldPos.Z); // see above
			ball.Hit.Vel.Set(oldVel.X, oldVel.Y, oldVel.Z);
			HitBBox.ZLow = oldZ.X; // HACK
			HitBBox.ZHigh = oldZ.Y; // dto.

			if (hitTime >= 0) {
				// transform hit normal back to world coordinate system
				coll.HitNormal.Set(_matrix.MultiplyVectorT(coll.HitNormal));
			}

			return hitTime;
		}

		public override void Collide(CollisionEvent coll, PlayerPhysics physics) {
			var ball = coll.Ball;
			var hitNormal = coll.HitNormal;

			var dot = -hitNormal.Dot(ball.Hit.Vel);
			ball.Hit.Collide3DWall(hitNormal, Elasticity, ElasticityFalloff, Friction, Scatter);

			// manage item-specific logic
			if (Obj != null && FireEvents && dot >= Threshold) {
				Obj.OnCollision?.Invoke(this, ball, dot);
			}
		}
	}
}
