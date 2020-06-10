using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Plunger
{
	public class Plunger : Item<PlungerData>, IRenderable, IHittable
	{
		public bool IsCollidable => true;
		public EventProxy EventProxy { get; private set; }

		public const float PlungerHeight = 50.0f;
		private readonly PlungerMeshGenerator _meshGenerator;
		private HitObject[] _hitObjects;

		public Plunger(PlungerData data) : base(data)
		{
			_meshGenerator = new PlungerMeshGenerator(data);
		}

		public Plunger(BinaryReader reader, string itemName) : this(new PlungerData(reader, itemName)) { }

		public void SetupPlayer(Player player, Table.Table table)
		{
			var zHeight = table.GetSurfaceHeight(Data.Surface, Data.Center.X, Data.Center.Y);
			EventProxy = new EventProxy(this);
			_hitObjects = new HitObject[] {new PlungerHit(Data, zHeight)};
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(20, table, origin, asRightHanded);
		}

		public HitObject[] GetHitShapes() => _hitObjects;
	}
}
