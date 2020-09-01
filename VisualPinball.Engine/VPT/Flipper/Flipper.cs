using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Flipper
{
	public class Flipper : Item<FlipperData>, IRenderable, IMovable, IHittable
	{
		public bool IsCollidable => true;

		private readonly FlipperMeshGenerator _meshGenerator;
		private FlipperHit _hit;

		public Flipper(FlipperData data) : base(data)
		{
			_meshGenerator = new FlipperMeshGenerator(Data);
		}

		public Flipper(BinaryReader reader, string itemName) : this(new FlipperData(reader, itemName))
		{
		}

		public static Flipper GetDefault(Table.Table table)
		{
			var flipperData = new FlipperData(table.GetNewName<Flipper>("Flipper"), table.Width / 2f, table.Height / 2f);
			return new Flipper(flipperData);
		}

		public void Init(Table.Table table)
		{
			_hit = new FlipperHit(Data, table, this);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		public HitObject[] GetHitShapes() => new HitObject[] { _hit };

		#region API
		// todo move to api
		public void RotateToEnd() {
			_hit.GetMoverObject().EnableRotateEvent = 1;
			_hit.GetMoverObject().SetSolenoidState(true);
		}

		// todo move to api
		public void RotateToStart() {
			_hit.GetMoverObject().EnableRotateEvent = -1;
			_hit.GetMoverObject().SetSolenoidState(false);
		}
		#endregion
	}
}
