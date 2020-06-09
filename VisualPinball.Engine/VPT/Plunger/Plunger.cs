using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Plunger
{
	public class Plunger : Item<PlungerData>, IRenderable
	{
		private readonly PlungerMeshGenerator _meshGenerator;

		public Plunger(PlungerData data) : base(data)
		{
			_meshGenerator = new PlungerMeshGenerator(data);
		}

		public Plunger(BinaryReader reader, string itemName) : this(new PlungerData(reader, itemName)) { }

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(20, table, origin, asRightHanded);
		}
	}
}
