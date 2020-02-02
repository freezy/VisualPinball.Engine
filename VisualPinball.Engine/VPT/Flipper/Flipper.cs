using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Flipper
{
	public class Flipper : Item<FlipperData>, IRenderable
	{
		private readonly FlipperMeshGenerator _meshGenerator;

		public Flipper(FlipperData data) : base(data)
		{
			_meshGenerator = new FlipperMeshGenerator(Data);
		}

		public Flipper(BinaryReader reader, string itemName) : this(new FlipperData(reader, itemName)) { }

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
