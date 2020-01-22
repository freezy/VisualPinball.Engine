using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Kicker
{
	public class Kicker : Item<KickerData>, IRenderable
	{
		private readonly KickerMeshGenerator _meshGenerator;

		public Kicker(BinaryReader reader, string itemName) : base(new KickerData(reader, itemName))
		{
			_meshGenerator = new KickerMeshGenerator(Data);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
