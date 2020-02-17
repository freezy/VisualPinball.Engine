using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Ball
{
	public class Ball
	{
		public int Id;
		public static int IdCounter = 0;

		public BallData Data;
		public BallState State;
		public BallHit Hit;

		public CollisionEvent Coll => Hit.Coll;
		public BallMover Mover => Hit.GetMoverObject();

		public Ball(int id, BallData data, BallState state, Vertex3D initialVelocity, Player player, Table.Table table)
		{
		}
	}
}
