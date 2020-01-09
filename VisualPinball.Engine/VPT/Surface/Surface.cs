using System.IO;

namespace VisualPinball.Engine.VPT.Surface
{
	public class Surface : Item<SurfaceData>
	{
		public Surface(BinaryReader reader, string itemName) : base(new SurfaceData(reader, itemName))
		{
		}
	}
}
