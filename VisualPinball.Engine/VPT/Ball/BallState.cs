using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Ball
{
	public class BallState
	{
		public readonly string Name;
		public readonly Vertex3D Pos;
		public readonly Matrix2D Orientation = new Matrix2D();
		public bool IsFrozen = false;

		public BallState(string name, Vertex3D pos)
		{
			Name = name;
			Pos = pos;
		}
	}
}
