using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Gate
{
	public class Gate : Item<GateData>, IRenderable
	{
		private readonly GateMeshGenerator _meshGenerator;

		public Gate(GateData data) : base(data)
		{
			_meshGenerator = new GateMeshGenerator(Data);
		}

		public Gate(BinaryReader reader, string itemName) : this(new GateData(reader, itemName)) { }

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
