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

using System;
using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using MathF = VisualPinball.Engine.Math.MathF;

namespace VisualPinball.Engine.VPT.Bumper
{
	public class Bumper : Item<BumperData>, IRenderable
	{
		public override string ItemGroupName => "Bumpers";

		private readonly BumperMeshGenerator _meshGenerator;

		public Bumper(BumperData data) : base(data)
		{
			_meshGenerator = new BumperMeshGenerator(Data);
		}

		public Bumper(BinaryReader reader, string itemName) : this(new BumperData(reader, itemName))
		{
		}

		public static Bumper GetDefault(Table.Table table)
		{
			var bumperData = new BumperData(table.GetNewName<Bumper>("Bumper"), table.Width / 2f, table.Height / 2f);
			return new Bumper(bumperData);
		}

		#region IRenderable

		Matrix3D IRenderable.TransformationMatrix(Table.Table table, Origin origin)
		{
			switch (origin) {
				case Origin.Original:
					var rotMatrix = new Matrix3D().RotateZMatrix(MathF.DegToRad(Data.Orientation));
					var transMatrix = new Matrix3D().SetTranslation(Data.Center.X, Data.Center.Y, 0f);
					return rotMatrix.Multiply(transMatrix);

				case Origin.Global:
					return Matrix3D.Identity;

				default:
					throw new ArgumentOutOfRangeException(nameof(origin), origin, "Unknown origin " + origin);
			}
		}

		public Mesh GetMesh(string id, Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
			=> _meshGenerator.GetMesh(id, table, origin);

		public PbrMaterial GetMaterial(string id, Table.Table table) => _meshGenerator.GetMaterial(id, table);

		#endregion
	}
}
