using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Ramp
{
	public class Ramp : Item<RampData>, IRenderable
	{
		private readonly RampMeshGenerator _meshGenerator;

		public Ramp(BinaryReader reader, string itemName) : base(new RampData(reader, itemName))
		{
			_meshGenerator = new RampMeshGenerator(Data);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, asRightHanded);
		}
	}
}
