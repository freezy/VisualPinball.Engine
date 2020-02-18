using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Flipper
{
	public class Flipper : Item<FlipperData>, IRenderable, IPlayable, IMovable, IHittable
	{
		public EventProxy EventProxy { get; private set; }

		private readonly FlipperMeshGenerator _meshGenerator;
		private readonly FlipperState _state;
		private FlipperHit _hit;

		public Flipper(FlipperData data) : base(data)
		{
			_meshGenerator = new FlipperMeshGenerator(Data);
			_state = new FlipperState(Data.Name, data.IsVisible, data.StartAngle, data.Center.Clone(), data.Material, data.Image, data.RubberMaterial);
		}

		public Flipper(BinaryReader reader, string itemName) : this(new FlipperData(reader, itemName)) { }

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		public void SetupPlayer(Player player, Table.Table table)
		{
			EventProxy = new EventProxy(this);
			_hit = new FlipperHit(Data, _state, EventProxy, table);
		}

		public IMoverObject GetMover() => _hit.GetMoverObject();
		public bool IsCollidable => true;

		public HitObject[] GetHitShapes() => new HitObject[] { _hit };
	}
}
