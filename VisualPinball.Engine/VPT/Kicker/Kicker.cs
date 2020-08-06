using System.IO;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Kicker
{
	public class Kicker : Item<KickerData>, IRenderable, IBallCreationPosition, IHittable
	{
		public bool IsCollidable => true;
		public EventProxy EventProxy { get; private set; }
		public KickerHit KickerHit => _hit;
		public string[] UsedMaterials => new[] { Data.Material };

		private readonly KickerMeshGenerator _meshGenerator;
		private KickerHit _hit;

		public Kicker(KickerData data) : base(data)
		{
			_meshGenerator = new KickerMeshGenerator(Data);
		}

		public Kicker(BinaryReader reader, string itemName) : this(new KickerData(reader, itemName))
		{
		}

		public static Kicker GetDefault(Table.Table table)
		{
			var kickerData = new KickerData(table.GetNewName<Kicker>("Kicker"), table.Width / 2f, table.Height / 2f);
			return new Kicker(kickerData);
		}


		public void Init(Table.Table table)
		{
			var height = table.GetSurfaceHeight(Data.Surface, Data.Center.X, Data.Center.Y) * table.GetScaleZ();

			// reduce the hit circle radius because only the inner circle of the kicker should start a hit event
			var radius = Data.Radius * (Data.LegacyMode ? Data.FallThrough ? 0.75f : 0.6f : 1f);
			_hit = new KickerHit(Data, radius, height, table); // height of kicker hit cylinder
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		public HitObject[] GetHitShapes()
		{
			return new HitObject[] {_hit};
		}

		public Vertex3D GetBallCreationPosition(Table.Table table)
		{
			var height = table.GetSurfaceHeight(Data.Surface, Data.Center.X, Data.Center.Y);
			return new Vertex3D(Data.Center.X, Data.Center.Y, height); // TODO get position from hit object
		}

		public Vertex3D GetBallCreationVelocity(Table.Table table)
		{
			return new Vertex3D(0.1f, 0, 0);
		}

		public void OnBallCreated(PlayerPhysics physics, Ball.Ball ball)
		{
			ball.Coll.HitFlag = true;                        // HACK: avoid capture leaving kicker
			var hitNormal = new Vertex3D(Constants.FloatMax, Constants.FloatMax, Constants.FloatMax); // unused due to newBall being true
			// TODO this.hit!.doCollide(physics, ball, hitNormal, false, true);
		}
	}
}
