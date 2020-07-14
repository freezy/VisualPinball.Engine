using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.HitTarget
{
	public class HitTarget : Item<HitTargetData>, IRenderable
	{
		public string[] UsedMaterials => new string[] { Data.Material, Data.PhysicsMaterial };

		private readonly HitTargetMeshGenerator _meshGenerator;

		public HitTarget(HitTargetData data) : base(data)
		{
			_meshGenerator = new HitTargetMeshGenerator(Data);
		}

		public HitTarget(BinaryReader reader, string itemName) : this(new HitTargetData(reader, itemName)) { }

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
