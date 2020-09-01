using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Engine.Physics
{
	public class LineSegSlingshot : LineSeg
	{
		public float Force = 0;
		public bool DoHitEvent = false;

		private readonly SurfaceData _surfaceData;
		private float _eventTimeReset = 0;

		public LineSegSlingshot(SurfaceData surfaceData, Vertex2D p1, Vertex2D p2, float zLow, float zHigh, ItemType itemType)
			: base(p1, p2, zLow, zHigh, itemType)
		{
			_surfaceData = surfaceData;
		}
	}
}
