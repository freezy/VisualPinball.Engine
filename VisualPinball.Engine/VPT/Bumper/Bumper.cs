using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Bumper
{
	public class Bumper : Item<BumperData>, IRenderable
	{
		private readonly BumperMeshGenerator _meshGenerator;

		public Bumper(BumperData data) : base(data)
		{
			_meshGenerator = new BumperMeshGenerator(Data);
		}

		public Bumper(BinaryReader reader, string itemName) : this(new BumperData(reader, itemName)) { }

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
