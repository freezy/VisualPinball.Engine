using System.Collections.Generic;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Table
{
	public class TableHitGenerator
	{
		private readonly Table _table;
		private readonly TableData _data;

		public TableHitGenerator(Table table)
		{
			_data = table.Data;
			_table = table;
		}

		public IEnumerable<HitObject> GenerateHitObjects()
		{
			var hitObjects = new List<HitObject> {

				// simple outer borders:
				new LineSeg(
					new Vertex2D(_data.Right, _data.Top),
					new Vertex2D(_data.Right, _data.Bottom),
					_data.TableHeight,
					_data.GlassHeight,
					ItemType.Table
				),
				new LineSeg(
					new Vertex2D(_data.Left, _data.Bottom),
					new Vertex2D(_data.Left, _data.Top),
					_data.TableHeight,
					_data.GlassHeight,
					ItemType.Table
				),
				new LineSeg(
					new Vertex2D(_data.Right, _data.Bottom),
					new Vertex2D(_data.Left, _data.Bottom),
					_data.TableHeight,
					_data.GlassHeight,
					ItemType.Table
				),
				new LineSeg(
					new Vertex2D(_data.Left, _data.Top),
					new Vertex2D(_data.Right, _data.Top),
					_data.TableHeight,
					_data.GlassHeight,
					ItemType.Table
				)
			};

			// glass
			var rgv3D = new[] {
				new Vertex3D(_data.Left, _data.Top, _data.GlassHeight),
				new Vertex3D(_data.Right, _data.Top, _data.GlassHeight),
				new Vertex3D(_data.Right, _data.Bottom, _data.GlassHeight),
				new Vertex3D(_data.Left, _data.Bottom, _data.GlassHeight)
			};
			var hit3DPoly = new Hit3DPoly(rgv3D, ItemType.Table);
			hit3DPoly.CalcHitBBox();
			hitObjects.Add(hit3DPoly);

			return hitObjects.ToArray();
		}

		public HitPlane GeneratePlayfieldHit() {
			var playfieldHit = new HitPlane(new Vertex3D(0, 0, 1), _data.TableHeight);
			playfieldHit
				.SetFriction(_data.GetFriction())
				.SetElasticity(_data.GetElasticity(), _data.GetElasticityFalloff())
				.SetScatter(MathF.DegToRad(_data.GetScatter()));
			playfieldHit.ItemIndex = _table.Index;
			playfieldHit.ItemVersion = _table.Version;
			return playfieldHit;
		}

		public HitPlane GenerateGlassHit()
		{
			var glassHit = new HitPlane(new Vertex3D(0, 0, -1), _data.GlassHeight);
			glassHit.SetElasticity(0.2f);
			glassHit.ItemIndex = _table.Index;
			glassHit.ItemVersion = _table.Version;
			return glassHit;
		}
	}
}
