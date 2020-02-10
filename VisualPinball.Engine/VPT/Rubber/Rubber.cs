using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Rubber
{
	public class Rubber : Item<RubberData>, IRenderable
	{
		private readonly RubberMeshGenerator _meshGenerator;

		public Rubber(RubberData data) : base(data)
		{
			_meshGenerator = new RubberMeshGenerator(Data);
		}

		public Rubber(BinaryReader reader, string itemName) : this(new RubberData(reader, itemName)) { }

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
