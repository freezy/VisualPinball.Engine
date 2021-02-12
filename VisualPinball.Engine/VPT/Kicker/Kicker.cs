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
	public class Kicker : Item<KickerData>, IRenderable, IBallCreationPosition, ISwitchable, ICoilable
	{
		public override string ItemName { get; } = "Kicker";
		public override string ItemGroupName { get; } = "Kickers";
		public override ItemType ItemType { get; } = ItemType.Kicker;

		public Vertex3D Position { get => new Vertex3D(Data.Center.X, Data.Center.Y, 0); set => Data.Center = new Vertex2D(value.X, value.Y); }
		public float RotationY { get => Data.Angle; set => Data.Angle = value; }

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

		public Vertex3D GetBallCreationPosition(Table.Table table)
		{
			var height = table.GetSurfaceHeight(Data.Surface, Data.Center.X, Data.Center.Y);
			return new Vertex3D(Data.Center.X, Data.Center.Y, height);
		}

		public Vertex3D GetBallCreationVelocity(Table.Table table)
		{
			return new Vertex3D(0.1f, 0, 0);
		}
	}
}
