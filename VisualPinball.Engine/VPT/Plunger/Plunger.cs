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
		public override string ItemName => "Plunger";
		public override string ItemGroupName => "Plungers";

		public const string PullCoilId = "c_pull";
		public const string FireCoilId = "c_autofire";

		public IEnumerable<GamelogicEngineCoil> AvailableCoils { get; } = new[] {
			new GamelogicEngineCoil(PullCoilId) {Description = "Pull back"},
			new GamelogicEngineCoil(FireCoilId) {Description = "Auto-fire"},
		};

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

		public RenderObject GetRenderObject(Table.Table table, string id = null, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return MeshGenerator.GetRenderObject(table, id, asRightHanded);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return MeshGenerator.GetRenderObjects(20, table, origin, asRightHanded);
		}

		#endregion
	}
}
