using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Plunger
{
	public class Plunger : Item<PlungerData>, IRenderable, IHittable
	{
		public bool IsCollidable => true;
		public EventProxy EventProxy { get; private set; }
		public PlungerHit PlungerHit { get; private set; }

		public const float PlungerHeight = 50.0f;
		public const float PlungerMass = 30.0f;
		public const int PlungerNormalize = 100;

		public readonly PlungerMeshGenerator MeshGenerator;

		private HitObject[] _hitObjects;

		public Plunger(PlungerData data) : base(data)
		{
			MeshGenerator = new PlungerMeshGenerator(data);
		}

		public Plunger(BinaryReader reader, string itemName) : this(new PlungerData(reader, itemName))
		{
		}

		public static Plunger GetDefault(Table.Table table)
		{
			var plungerData = new PlungerData(table.GetNewName<Plunger>("Plunger"), table.Width / 2f, table.Height / 2f);
			return new Plunger(plungerData);
		}

		public void Init(Table.Table table)
		{
			var zHeight = table.GetSurfaceHeight(Data.Surface, Data.Center.X, Data.Center.Y);
			EventProxy = new EventProxy(this);
			PlungerHit = new PlungerHit(Data, zHeight);
			_hitObjects = new HitObject[] { PlungerHit };
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return MeshGenerator.GetRenderObjects(20, table, origin, asRightHanded);
		}

		public HitObject[] GetHitShapes() => _hitObjects;
	}
}
