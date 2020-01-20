using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.HitTarget
{
	public class HitTarget : Item<HitTargetData>, IRenderable
	{
		private readonly HitTargetMeshGenerator _meshGenerator;

		public HitTarget(BinaryReader reader, string itemName) : base(new HitTargetData(reader, itemName))
		{
			_meshGenerator = new HitTargetMeshGenerator(Data);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
