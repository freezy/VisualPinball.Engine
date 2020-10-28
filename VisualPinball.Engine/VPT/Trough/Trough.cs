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

using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Trough
{
	public class Trough : Item<TroughData>, IRenderable
	{
		public override string ItemName { get; } = "Trough";
		public override string ItemGroupName { get; } = "Troughs";

		public Vertex3D Position { get => Vertex3D.Zero; set { } }
		public float RotationY { get => 0f; set { } }

		public Trough(TroughData data) : base(data)
		{
		}

		public Trough(BinaryReader reader, string itemName) : this(new TroughData(reader, itemName))
		{
		}

		#region IRenderable

		Matrix3D IRenderable.TransformationMatrix(Table.Table table, Origin origin)
		{
			return Matrix3D.Identity;
		}

		public RenderObject GetRenderObject(Table.Table table, string id = null, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return null;
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return new RenderObjectGroup(Data.Name, "trough", Math.Matrix3D.Identity, new RenderObject[0]);
		}

		#endregion
	}
}
