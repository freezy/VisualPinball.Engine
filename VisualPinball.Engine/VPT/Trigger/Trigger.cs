using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Trigger
{
	public class Trigger : Item<TriggerData>, IRenderable
	{
		private readonly TriggerMeshGenerator _meshGenerator;

		public Trigger(TriggerData data) : base(data)
		{
			_meshGenerator = new TriggerMeshGenerator(Data);
		}

		public Trigger(BinaryReader reader, string itemName) : this(new TriggerData(reader, itemName)) { }

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
