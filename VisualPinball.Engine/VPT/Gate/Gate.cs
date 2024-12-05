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

namespace VisualPinball.Engine.VPT.Gate
{
	public class Gate : Item<GateData>, IRenderable
	{
		public override string ItemGroupName => "Gates";

		private readonly GateMeshGenerator _meshGenerator;

		public Gate(GateData data) : base(data)
		{
			_meshGenerator = new GateMeshGenerator(Data);
		}

		public Gate(BinaryReader reader, string itemName) : this(new GateData(reader, itemName))
		{
		}

		public static Gate GetDefault(Table.Table table)
		{
			var gateData = new GateData(table.GetNewName<Gate>("Gate"), table.Width / 2f, table.Height / 2f);
			return new Gate(gateData);
		}

		#region IRenderable

		Matrix3D IRenderable.TransformationMatrix(Table.Table table, Origin origin) => _meshGenerator.GetPostMatrix(table, origin);
		public Mesh GetMesh(string id, Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
			=> _meshGenerator.GetMesh(id, table, origin, asRightHanded);

		public PbrMaterial GetMaterial(string id, Table.Table table)
			=> _meshGenerator.GetMaterial(id, table);

		#endregion

		public override string ToString() => $"Gate[{Data.Name}/{Data.GateType} at {Data.Center}]";
	}
}
