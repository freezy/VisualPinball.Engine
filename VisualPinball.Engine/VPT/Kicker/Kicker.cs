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

namespace VisualPinball.Engine.VPT.Kicker
{
	public class Kicker : Item<KickerData>, IRenderable, ISwitchable, ICoilable
	{
		public override string ItemName => "Kicker";
		public override string ItemGroupName => "Kickers";

		public bool IsPulseSwitch => false;

		public string[] UsedMaterials => new[] { Data.Material };

		private readonly KickerMeshGenerator _meshGenerator;

		public Kicker(KickerData data) : base(data)
		{
			_meshGenerator = new KickerMeshGenerator(Data);
		}

		public Kicker(BinaryReader reader, string itemName) : this(new KickerData(reader, itemName))
		{
		}

		public static Kicker GetDefault(Table.Table table)
		{
			var kickerData = new KickerData(table.GetNewName<Kicker>("Kicker"), table.Width / 2f, table.Height / 2f);
			return new Kicker(kickerData);
		}

		#region IRenderable

		Matrix3D IRenderable.TransformationMatrix(Table.Table table, Origin origin) => _meshGenerator.GetPostMatrix(table, origin);

		public RenderObject GetRenderObject(Table.Table table, string id = null, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObject(table, origin, asRightHanded);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		#endregion

		public bool IsDualWound { get; set; }
	}
}
