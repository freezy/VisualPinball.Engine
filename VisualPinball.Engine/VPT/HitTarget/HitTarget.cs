using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.HitTarget
{
	public class HitTarget : Item<HitTargetData>, IRenderable, IHittable
	{
		public bool IsCollidable => true;
		public HitObject[] GetHitShapes() => _hits;
		public EventProxy EventProxy { get; private set; }

		private readonly HitTargetMeshGenerator _meshGenerator;
		private readonly HitTargetHitGenerator _hitGenerator;
		private HitObject[] _hits;

		public HitTarget(HitTargetData data) : base(data)
		{
			_meshGenerator = new HitTargetMeshGenerator(Data);
			_hitGenerator = new HitTargetHitGenerator(Data, _meshGenerator);
		}

		public HitTarget(BinaryReader reader, string itemName) : this(new HitTargetData(reader, itemName))
		{
		}

		public static HitTarget GetDefault(Table.Table table)
		{
			var hitTargetData = new HitTargetData(table.GetNewName<HitTarget>("Target"), table.Width / 2f, table.Height / 2f);
			return new HitTarget(hitTargetData);
		}

		public void Init(Table.Table table)
		{
			EventProxy = new EventProxy(this);
			_hits = _hitGenerator.GenerateHitObjects(table);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
