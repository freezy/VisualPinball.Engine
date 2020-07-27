using System.IO;
using System.Linq;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Gate
{
	public class Gate : Item<GateData>, IRenderable, IMovable, IHittable
	{
		public EventProxy EventProxy { get; private set; }
		public bool IsCollidable => true;

		private readonly GateMeshGenerator _meshGenerator;
		private readonly GateHitGenerator _hitGenerator;
		private GateHit _hitGate;
		private LineSeg[] _hitLines;
		private HitCircle[] _hitCircles;

		public Gate(GateData data) : base(data)
		{
			_meshGenerator = new GateMeshGenerator(Data);
			_hitGenerator = new GateHitGenerator(Data);
		}

		public Gate(BinaryReader reader, string itemName) : this(new GateData(reader, itemName)) { }

		public void Init(Table.Table table)
		{
			var height = table.GetSurfaceHeight(Data.Surface, Data.Center.X, Data.Center.Y);
			var radAngle = MathF.DegToRad(Data.Rotation);
			var tangent = new Vertex2D(MathF.Cos(radAngle), MathF.Sin(radAngle));

			EventProxy = new EventProxy(this);
			_hitGate = _hitGenerator.GenerateGateHit(EventProxy, height);
			_hitLines = _hitGenerator.GenerateLineSegs(height, tangent);
			_hitCircles = _hitGenerator.GenerateBracketHits(height, tangent);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		public HitObject[] GetHitShapes()
		{
			return new HitObject[] {_hitGate}
				.Concat(_hitLines)
				.Concat(_hitCircles)
				.ToArray();
		}

		public IMoverObject GetMover()
		{
			// not needed in unity ECS
			throw new System.NotImplementedException();
		}
	}
}
