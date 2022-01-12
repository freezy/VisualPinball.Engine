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

namespace VisualPinball.Engine.VPT.MetalWireGuide
{
	public class MetalWireGuide : Item<MetalWireGuideData>, IRenderable
	{
		public override string ItemGroupName => "MetalWireGuides";

		public readonly MetalWireGuideMeshGenerator MeshGenerator;

		public MetalWireGuide(MetalWireGuideData data) : base(data)
		{
			MeshGenerator = new MetalWireGuideMeshGenerator(Data);
		}

		public MetalWireGuide(BinaryReader reader, string itemName) : this(new MetalWireGuideData(reader, itemName))
		{
		}

		public static MetalWireGuide GetDefault(Table.Table table)
		{
			var x = table.Width / 2f;
			var y = table.Height / 2f;
			var metalWireGuideData = new MetalWireGuideData(table.GetNewName<MetalWireGuide>("MetalWireGuide")) {
				DragPoints = new[] {
					new DragPointData(x, y - 100f) {IsSmooth = true },
					new DragPointData(x, y) {IsSmooth = true },
				}
			};
			return new MetalWireGuide(metalWireGuideData);
		}

		#region IRenderable

		Matrix3D IRenderable.TransformationMatrix(Table.Table table, Origin origin) => Matrix3D.Identity;
		public Mesh GetMesh(string id, Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return MeshGenerator.GetMesh(table, Data);
		}

		public PbrMaterial GetMaterial(string id, Table.Table table) => MeshGenerator.GetMaterial(table, Data);

		#endregion
	}
}
