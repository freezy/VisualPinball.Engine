using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Ball
{
	public class BallState
	{
		public Vertex3D Pos = new Vertex3D();
		public Matrix2D Orientation = new Matrix2D();
		public bool IsFrozen = false;

		public BallState(string name, Vertex3D pos)
		{
		}
	}
}
