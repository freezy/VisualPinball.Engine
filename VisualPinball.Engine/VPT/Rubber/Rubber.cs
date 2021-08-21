// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

namespace VisualPinball.Engine.VPT.Rubber
{
	public class Rubber : Item<RubberData>, IRenderable
	{
		public override string ItemName => "Rubber";
		public override string ItemGroupName => "Rubbers";

		public readonly RubberMeshGenerator MeshGenerator;

		public Rubber(RubberData data) : base(data)
		{
			MeshGenerator = new RubberMeshGenerator(Data);
		}

		public Rubber(BinaryReader reader, string itemName) : this(new RubberData(reader, itemName))
		{
		}

		public static Rubber GetDefault(Table.Table table)
		{
			var x = table.Width / 2f;
			var y = table.Height / 2f;
			var rubberData = new RubberData(table.GetNewName<Rubber>("Rubber")) {
				DragPoints = new[] {
					new DragPointData(x, y - 50f) {IsSmooth = true },
					new DragPointData(x - 50f * MathF.Cos(MathF.PI / 4), y - 50f * MathF.Sin(MathF.PI / 4)) {IsSmooth = true },
					new DragPointData(x - 50f, y) {IsSmooth = true },
					new DragPointData(x - 50f * MathF.Cos(MathF.PI / 4), y + 50f * MathF.Sin(MathF.PI / 4)) {IsSmooth = true },
					new DragPointData(x, y + 50f) {IsSmooth = true },
					new DragPointData(x + 50f * MathF.Cos(MathF.PI / 4), y + 50f * MathF.Sin(MathF.PI / 4)) {IsSmooth = true },
					new DragPointData(x + 50f, y) {IsSmooth = true },
					new DragPointData(x + 50f * MathF.Cos(MathF.PI / 4), y - 50f * MathF.Sin(MathF.PI / 4)) {IsSmooth = true },
				}
			};
			return new Rubber(rubberData);
		}

		#region IRenderable

		Matrix3D IRenderable.TransformationMatrix(Table.Table table, Origin origin) => MeshGenerator.GetPostMatrix(table, origin);

		public RenderObject GetRenderObject(Table.Table table, string id = null, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return MeshGenerator.GetRenderObject(table, origin, asRightHanded);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return MeshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		#endregion
	}
}
