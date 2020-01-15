using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Rubber
{
	public class Rubber : Item<RubberData>, IRenderable
	{
		private readonly RubberMeshGenerator _meshGenerator;

		public Rubber(BinaryReader reader, string itemName) : base(new RubberData(reader, itemName))
		{
			_meshGenerator = new RubberMeshGenerator(Data);
		}

		public RenderObject[] GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
