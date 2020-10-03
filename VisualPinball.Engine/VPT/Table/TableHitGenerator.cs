// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

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

		public IEnumerable<HitObject> GenerateHitObjects(IItem item)
		{
			var hitObjects = new List<HitObject> {

				// simple outer borders:
				new LineSeg(
					new Vertex2D(_data.Right, _data.Top),
					new Vertex2D(_data.Right, _data.Bottom),
					_table.TableHeight,
					_table.GlassHeight,
					ItemType.Table,
					item
				),
				new LineSeg(
					new Vertex2D(_data.Left, _data.Bottom),
					new Vertex2D(_data.Left, _data.Top),
					_table.TableHeight,
					_table.GlassHeight,
					ItemType.Table,
					item
				),
				new LineSeg(
					new Vertex2D(_data.Right, _data.Bottom),
					new Vertex2D(_data.Left, _data.Bottom),
					_table.TableHeight,
					_table.GlassHeight,
					ItemType.Table,
					item
				),
				new LineSeg(
					new Vertex2D(_data.Left, _data.Top),
					new Vertex2D(_data.Right, _data.Top),
					_table.TableHeight,
					_table.GlassHeight,
					ItemType.Table,
					item
				)
			};

			// glass
			var rgv3D = new[] {
				new Vertex3D(_data.Left, _data.Top, _table.GlassHeight),
				new Vertex3D(_data.Right, _data.Top, _table.GlassHeight),
				new Vertex3D(_data.Right, _data.Bottom, _table.GlassHeight),
				new Vertex3D(_data.Left, _data.Bottom, _table.GlassHeight)
			};
			var hit3DPoly = new Hit3DPoly(rgv3D, ItemType.Table, item);
			hit3DPoly.CalcHitBBox();
			hitObjects.AddRange(hit3DPoly.ConvertToTriangles());

			foreach (var hitObject in hitObjects) {
				hitObject.ItemIndex = _table.Index;
				hitObject.ItemVersion = _table.Version;
			}

			return hitObjects;
		}

		public HitPlane GeneratePlayfieldHit(IItem item) {
			var playfieldHit = new HitPlane(new Vertex3D(0, 0, 1), _table.TableHeight, item);
			playfieldHit
				.SetFriction(_data.GetFriction())
				.SetElasticity(_data.GetElasticity(), _data.GetElasticityFalloff())
				.SetScatter(MathF.DegToRad(_data.GetScatter()));
			playfieldHit.ItemIndex = _table.Index;
			playfieldHit.ItemVersion = _table.Version;
			return playfieldHit;
		}

		public HitPlane GenerateGlassHit(IItem item)
		{
			var glassHit = new HitPlane(new Vertex3D(0, 0, -1), _table.GlassHeight, item);
			glassHit.SetElasticity(0.2f);
			glassHit.ItemIndex = _table.Index;
			glassHit.ItemVersion = _table.Version;
			return glassHit;
		}
	}
}
