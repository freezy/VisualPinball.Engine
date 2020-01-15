using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Surface
{
	public class Surface : Item<SurfaceData>, IRenderable
	{
		private readonly SurfaceMeshGenerator _meshGenerator;
		public Surface(BinaryReader reader, string itemName) : base(new SurfaceData(reader, itemName))
		{
			_meshGenerator = new SurfaceMeshGenerator(Data);
		}

		public RenderObject[] GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, asRightHanded);
		}
	}
}
