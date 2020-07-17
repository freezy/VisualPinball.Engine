using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public class HitTriangle : HitObject
	{
		public readonly Vertex3D[] Rgv;
		public readonly Vertex3D Normal;

		public bool IsDegenerate => Normal.IsZero();

		public HitTriangle(Vertex3D[] rgv, ItemType itemType) : base (itemType)
		{
			Rgv = rgv;
			/* NB: due to the swapping of the order of e0 and e1,
			 * the vertices must be passed in counterclockwise order
			 * (but rendering uses clockwise order!)
			 */
			var e0 = Rgv[2].Clone().Sub(Rgv[0]);
			var e1 = Rgv[1].Clone().Sub(Rgv[0]);
			Normal = Vertex3D.CrossProduct(e0, e1);
			Normal.NormalizeSafe();

			Elasticity = 0.3f;
			SetFriction(0.3f);
			Scatter = 0;
		}

		public override void CalcHitBBox()
		{
			HitBBox.Left = System.Math.Min(Rgv[0].X, System.Math.Min(Rgv[1].X, Rgv[2].X));
			HitBBox.Right = System.Math.Max(Rgv[0].X, System.Math.Max(Rgv[1].X, Rgv[2].X));
			HitBBox.Top = System.Math.Min(Rgv[0].Y, System.Math.Min(Rgv[1].Y, Rgv[2].Y));
			HitBBox.Bottom = System.Math.Max(Rgv[0].Y, System.Math.Max(Rgv[1].Y, Rgv[2].Y));
			HitBBox.ZLow = System.Math.Min(Rgv[0].Z, System.Math.Min(Rgv[1].Z, Rgv[2].Z));
			HitBBox.ZHigh = System.Math.Max(Rgv[0].Z, System.Math.Max(Rgv[1].Z, Rgv[2].Z));
		}

		public override float HitTest(Ball ball, float dTime, CollisionEvent coll, PlayerPhysics physics)
		{
			// not needed in unity ECS
			throw new System.NotImplementedException();
		}

		public override void Collide(CollisionEvent coll, PlayerPhysics physics)
		{
			// not needed in unity ECS
			throw new System.NotImplementedException();
		}
	}
}
