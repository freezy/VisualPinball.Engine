using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Ramp
{
	public class Ramp : Item<RampData>, IRenderable, IHittable
	{
		public bool IsCollidable => true;
		public EventProxy EventProxy { get; private set; }
		public HitObject[] GetHitShapes() => _hits;

		private readonly RampMeshGenerator _meshGenerator;
		private readonly RampHitGenerator _hitGenerator;
		private HitObject[] _hits;

		public Ramp(RampData data) : base(data)
		{
			_meshGenerator = new RampMeshGenerator(Data);
			_hitGenerator = new RampHitGenerator(Data, _meshGenerator);
		}

		public Ramp(BinaryReader reader, string itemName) : this(new RampData(reader, itemName))
		{
		}

		public static Ramp GetDefault(Table.Table table)
		{
			var rampData = new RampData(table.GetNewName<Ramp>("Ramp"), new[] {
				new DragPointData(table.Width / 2f, table.Height / 2f + 200f) { HasAutoTexture = false, IsSmooth = true },
				new DragPointData(table.Width / 2f, table.Height / 2f - 200f) { HasAutoTexture = false, IsSmooth = true }
			}) {
				HeightTop = 50f,
				HeightBottom = 0f,
				WidthTop = 60f,
				WidthBottom = 75f
			};
			return new Ramp(rampData);
		}

		public void Init(Table.Table table)
		{
			EventProxy = new EventProxy(this);
			_hits = _hitGenerator.GenerateHitObjects(table, EventProxy);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, asRightHanded);
		}

		public float GetSurfaceHeight(float x, float y, Table.Table table)
		{
			var vVertex = _meshGenerator.GetCentralCurve(table);
			Mesh.ClosestPointOnPolygon(vVertex, new Vertex2D(x, y), false, out var vOut, out var iSeg);

			if (iSeg == -1) {
				return 0.0f; // Object is not on ramp path
			}

			// Go through vertices (including iSeg itself) counting control points until iSeg
			var totalLength = 0.0f;
			var startLength = 0.0f;

			var cVertex = vVertex.Length;
			for (var i2 = 1; i2 < cVertex; i2++) {
				var vDx = vVertex[i2].X - vVertex[i2 - 1].X;
				var vDy = vVertex[i2].Y - vVertex[i2 - 1].Y;
				var vLen = MathF.Sqrt(vDx * vDx + vDy * vDy);
				if (i2 <= iSeg) {
					startLength += vLen;
				}
				totalLength += vLen;
			}

			var dx = vOut.X - vVertex[iSeg].X;
			var dy = vOut.Y - vVertex[iSeg].Y;
			var len = MathF.Sqrt(dx * dx + dy * dy);
			startLength += len; // Add the distance the object is between the two closest polyline segments.  Matters mostly for straight edges. Z does not respect that yet!

			var topHeight = Data.HeightTop + table.TableHeight;
			var bottomHeight = Data.HeightBottom + table.TableHeight;

			return vVertex[iSeg].Z + startLength / totalLength * (topHeight - bottomHeight) + bottomHeight;
		}
	}
}
