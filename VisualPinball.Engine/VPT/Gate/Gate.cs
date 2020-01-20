using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Gate
{
	public class Gate : Item<GateData>, IRenderable
	{
		private readonly GateMeshGenerator _meshGenerator;

		public Gate(BinaryReader reader, string itemName) : base(new GateData(reader, itemName))
		{
			_meshGenerator = new GateMeshGenerator(Data);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
