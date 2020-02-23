using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Ball
{
	public class Ball
	{
		public uint Id => Data.Id;
		public string Name => Data.GetName();

		public readonly BallData Data;
		public readonly BallState State;
		public readonly BallHit Hit;

		public static uint IdCounter = 0;

		public CollisionEvent Coll => Hit.Coll;
		public BallMover Mover => Hit.GetMoverObject();

		public Ball(BallData data, BallState state, Vertex3D initialVelocity, Player player, Table.Table table)
		{
			Data = data;
			State = state;
			Hit = new BallHit(this, data, state, initialVelocity, table.Data);
		}
	}
}
