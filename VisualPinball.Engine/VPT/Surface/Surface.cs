using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Surface
{
	public class Surface : Item<SurfaceData>, IRenderable
	{
		private readonly SurfaceMeshGenerator _meshGenerator;

		public Surface(SurfaceData data) : base(data)
		{
			_meshGenerator = new SurfaceMeshGenerator(Data);
		}

		public Surface(BinaryReader reader, string itemName) : this(new SurfaceData(reader, itemName)) { }

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, asRightHanded);
		}
	}
}
