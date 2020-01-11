using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Flipper
{
	public class Flipper : Item<FlipperData>, IRenderable
	{
		private readonly FlipperMeshGenerator _meshGenerator;

		public Flipper(BinaryReader reader, string itemName) : base(new FlipperData(reader, itemName))
		{
			_meshGenerator = new FlipperMeshGenerator(Data);
		}

		public RenderObject[] GetRenderObjects(Table.Table table)
		{
			return _meshGenerator.GetRenderObjects(table);
		}
	}
}
