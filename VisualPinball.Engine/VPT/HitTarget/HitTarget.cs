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

namespace VisualPinball.Engine.VPT.HitTarget
{
	public class HitTarget : Item<HitTargetData>, IRenderable
	{
		public override string ItemGroupName => "Targets";

		public HitTarget(HitTargetData data) : base(data)
		{
		}

		public HitTarget(BinaryReader reader, string itemName) : this(new HitTargetData(reader, itemName))
		{
		}

		public static HitTarget GetHitTarget(Table.Table table)
		{
			var hitTargetData = new HitTargetData(table.GetNewName<HitTarget>("HitTarget"), table.Width / 2f, table.Height / 2f) {
				TargetType = TargetType.HitFatTargetRectangle
			};
			return new HitTarget(hitTargetData);
		}


		public static HitTarget GetDropTarget(Table.Table table)
		{
			var hitTargetData = new HitTargetData(table.GetNewName<HitTarget>("DropTarget"), table.Width / 2f, table.Height / 2f) {
				TargetType = TargetType.DropTargetBeveled
			};
			return new HitTarget(hitTargetData);
		}

		#region IRenderable

		Matrix3D IRenderable.TransformationMatrix(Table.Table table, Origin origin) => new HitTargetMeshGenerator(Data, table).GetPostMatrix(table, origin);
		public Mesh GetMesh(string id, Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
			=> new HitTargetMeshGenerator(Data, table).GetMesh(origin, asRightHanded);

		public PbrMaterial GetMaterial(string id, Table.Table table)
			=> new HitTargetMeshGenerator(Data, table).GetMaterial();

		#endregion
	}
}
