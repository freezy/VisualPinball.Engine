using System.IO;
using System.Linq;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Gate
{
	public class Gate : Item<GateData>, IRenderable, IHittable
	{
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

		public Gate(BinaryReader reader, string itemName) : this(new GateData(reader, itemName))
		{
		}

		public static Gate GetDefault(Table.Table table)
		{
			var gateData = new GateData(table.GetNewName<Gate>("Gate"), table.Width / 2f, table.Height / 2f);
			return new Gate(gateData);
		}

		public void Init(Table.Table table)
		{
			var height = table.GetSurfaceHeight(Data.Surface, Data.Center.X, Data.Center.Y);
			var radAngle = MathF.DegToRad(Data.Rotation);
			var tangent = new Vertex2D(MathF.Cos(radAngle), MathF.Sin(radAngle));

			_hitGate = _hitGenerator.GenerateGateHit(height, this);
			_hitLines = _hitGenerator.GenerateLineSegs(height, tangent, this);
			_hitCircles = _hitGenerator.GenerateBracketHits(height, tangent, this);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		public HitObject[] GetHitShapes()
		{
			return new HitObject[] {_hitGate}
				.Concat(_hitLines ?? new HitObject[0])
				.Concat(_hitCircles ?? new HitObject[0])
				.ToArray();
		}
	}
}
