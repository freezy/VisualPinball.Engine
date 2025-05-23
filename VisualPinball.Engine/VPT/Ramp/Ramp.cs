// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Ramp
{
	public class Ramp : Item<RampData>, IRenderable
	{
		public override string ItemGroupName => "Ramps";

		public readonly RampMeshGenerator MeshGenerator;

		public Ramp(RampData data) : base(data)
		{
			MeshGenerator = new RampMeshGenerator(Data, Vertex3D.Zero);
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

		#region IRenderable

		Matrix3D IRenderable.TransformationMatrix(Table.Table table, Origin origin) => Matrix3D.Identity;
		public Mesh GetMesh(string id, Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
			=> MeshGenerator.GetMesh(id, table, asRightHanded);

		public PbrMaterial GetMaterial(string id, Table.Table table) => MeshGenerator.GetMaterial(table, Data);

		#endregion

		public float GetSurfaceHeight(float x, float y, Table.Table table)
		{
			var vVertex = MeshGenerator.GetCentralCurve();
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

			var topHeight = Data.HeightTop;
			var bottomHeight = Data.HeightBottom;

			return vVertex[iSeg].Z + startLength / totalLength * (topHeight - bottomHeight) + bottomHeight;
		}
	}
}
