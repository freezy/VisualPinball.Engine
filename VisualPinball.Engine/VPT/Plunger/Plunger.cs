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

using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Plunger
{
	public class Plunger : Item<PlungerData>, IRenderable
	{
		public override string ItemGroupName => "Plungers";

		public const float PlungerHeight = 50.0f;
		public const float PlungerMass = 30.0f;
		public const int PlungerNormalize = 100;

		public readonly PlungerMeshGenerator MeshGenerator;

		public Plunger(PlungerData data) : base(data)
		{
			MeshGenerator = new PlungerMeshGenerator(data);
		}

		public Plunger(BinaryReader reader, string itemName) : this(new PlungerData(reader, itemName))
		{
		}

		public static Plunger GetDefault(Table.Table table)
		{
			var plungerData = new PlungerData(table.GetNewName<Plunger>("Plunger"), table.Width / 2f, table.Height / 2f) {
				Type = PlungerType.PlungerTypeCustom
			};
			return new Plunger(plungerData);
		}

		#region IRenderable

		Matrix3D IRenderable.TransformationMatrix(Table.Table table, Origin origin) => Matrix3D.Identity;

		public Mesh GetMesh(string id, Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
			=> MeshGenerator.GetMesh(id, 20, table, origin, asRightHanded);

		public PbrMaterial GetMaterial(string id, Table.Table table)
			=> MeshGenerator.GetMaterial(table);

		#endregion
	}
}
