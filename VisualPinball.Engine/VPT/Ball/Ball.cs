using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Ball
{
	public class Ball
	{
		public uint Id => Data.Id;
		public string Name => Data.GetName();

		public readonly BallData Data;

		public static uint IdCounter = 0;

		public Ball(BallData data, Vertex3D initialVelocity, Table.Table table)
		{
			Data = data;
		}
	}
}
