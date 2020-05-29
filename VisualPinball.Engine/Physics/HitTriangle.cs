using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public class HitTriangle : HitObject
	{
		public readonly Vertex3D[] Rgv;
		public readonly Vertex3D Normal;

		public HitTriangle(Vertex3D[] rgv)
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
			throw new System.NotImplementedException();
		}

		public override float HitTest(Ball ball, float dTime, CollisionEvent coll, PlayerPhysics physics)
		{
			throw new System.NotImplementedException();
		}

		public override void Collide(CollisionEvent coll, PlayerPhysics physics)
		{
			throw new System.NotImplementedException();
		}
	}
}
