using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Surface
{
	public class Surface : Item<SurfaceData>, IRenderable, IHittable
	{
		public EventProxy EventProxy { get; private set; }
		public bool IsCollidable => Data.IsCollidable;

		public HitObject[] GetHitShapes() => _hits;

		private readonly SurfaceMeshGenerator _meshGenerator;
		private readonly SurfaceHitGenerator _hitGenerator;
		private HitObject[] _hits;

		public Surface(SurfaceData data) : base(data)
		{
			_meshGenerator = new SurfaceMeshGenerator(Data);
			_hitGenerator = new SurfaceHitGenerator(Data);
		}

		public Surface(BinaryReader reader, string itemName) : this(new SurfaceData(reader, itemName)) { }

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, asRightHanded);
		}

		public void Init(Table.Table table)
		{
			EventProxy = new EventProxy(this);
			_hits = _hitGenerator.GenerateHitObjects(EventProxy, table);
		}
	}
}
