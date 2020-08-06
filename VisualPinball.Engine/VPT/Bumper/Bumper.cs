using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Bumper
{
	public class Bumper : Item<BumperData>, IRenderable, IHittable
	{
		public bool IsCollidable => true;
		public EventProxy EventProxy { get; private set; }

		private readonly BumperMeshGenerator _meshGenerator;

		private HitObject[] _hits;

		public Bumper(BumperData data) : base(data)
		{
			_meshGenerator = new BumperMeshGenerator(Data);
		}

		public Bumper(BinaryReader reader, string itemName) : this(new BumperData(reader, itemName))
		{
		}

		public static Bumper GetDefault(Table.Table table)
		{
			var bumperData = new BumperData(table.GetNewName<Bumper>("Bumper"), table.Width / 2f, table.Height / 2f);
			return new Bumper(bumperData);
		}

		public void Init(Table.Table table)
		{
			var height = table.GetSurfaceHeight(Data.Surface, Data.Center.X, Data.Center.Y);
			_hits = new HitObject[] {new BumperHit(Data, height)};
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		public HitObject[] GetHitShapes() => _hits;
	}
}
