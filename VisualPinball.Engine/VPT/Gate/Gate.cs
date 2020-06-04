using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Gate
{
	public class Gate : Item<GateData>, IRenderable, IMovable, IHittable
	{
		public EventProxy EventProxy { get; private set; }
		public bool IsCollidable => true;

		private readonly GateMeshGenerator _meshGenerator;

		public Gate(GateData data) : base(data)
		{
			_meshGenerator = new GateMeshGenerator(Data);
		}

		public Gate(BinaryReader reader, string itemName) : this(new GateData(reader, itemName)) { }

		public void SetupPlayer(Player player, Table.Table table)
		{
			throw new System.NotImplementedException();
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		public IMoverObject GetMover()
		{
			throw new System.NotImplementedException();
		}

		public HitObject[] GetHitShapes()
		{
			throw new System.NotImplementedException();
		}
	}
}
