using System.IO;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Kicker
{
	public class Kicker : Item<KickerData>, IRenderable, IBallCreationPosition
	{
		private readonly KickerMeshGenerator _meshGenerator;

		public Kicker(KickerData data) : base(data)
		{
			_meshGenerator = new KickerMeshGenerator(Data);
		}

		public Kicker(BinaryReader reader, string itemName) : this(new KickerData(reader, itemName)) { }

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
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
