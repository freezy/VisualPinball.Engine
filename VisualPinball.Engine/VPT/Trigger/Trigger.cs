using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Trigger
{
	public class Trigger : Item<TriggerData>, IRenderable
	{
		private readonly TriggerMeshGenerator _meshGenerator;

		public Trigger(BinaryReader reader, string itemName) : base(new TriggerData(reader, itemName))
		{
			_meshGenerator = new TriggerMeshGenerator(Data);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
