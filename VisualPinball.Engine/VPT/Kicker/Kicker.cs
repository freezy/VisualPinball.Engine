using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Kicker
{
	public class Kicker : Item<KickerData>, IRenderable
	{
		private readonly KickerMeshGenerator _meshGenerator;

		public Kicker(KickerData data) : base(data)
		{
			_meshGenerator = new KickerMeshGenerator(Data);
		}

		public Kicker(BinaryReader reader, string itemName) : this(new KickerData(reader, itemName)) { }

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
