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

		public static uint IdCounter = 0;

		public Ball(BallData data, BallState state, Vertex3D initialVelocity, Table.Table table)
		{
			Data = data;
			State = state;
		}
	}
}
