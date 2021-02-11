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

namespace VisualPinball.Engine.VPT.Trigger
{
	public class Trigger : Item<TriggerData>, IRenderable, ISwitchable
	{
		public override string ItemName { get; } = "Trigger";
		public override string ItemGroupName { get; } = "Triggers";
		public override ItemType ItemType { get; } = ItemType.Trigger;

		public Vertex3D Position { get => new Vertex3D(Data.Center.X, Data.Center.Y, 0); set => Data.Center = new Vertex2D(value.X, value.Y); }
		public float RotationY { get => Data.Rotation; set => Data.Rotation = value; }

		public bool IsPulseSwitch => false;

		public bool IsCollidable => Data.IsEnabled;

		private readonly TriggerMeshGenerator _meshGenerator;

		public Trigger(TriggerData data) : base(data)
		{
			_meshGenerator = new TriggerMeshGenerator(Data);
		}

		public Trigger(BinaryReader reader, string itemName) : this(new TriggerData(reader, itemName))
		{
		}

		public static Trigger GetDefault(Table.Table table)
		{
			var triggerData = new TriggerData(table.GetNewName<Trigger>("Trigger"), table.Width / 2f, table.Height / 2f)
			{
				DragPoints = new[] {
					new DragPointData(table.Width / 2f - 50f, table.Height / 2f - 50f),
					new DragPointData(table.Width / 2f - 50f, table.Height / 2f + 50f),
					new DragPointData(table.Width / 2f + 50f, table.Height / 2f + 50f),
					new DragPointData(table.Width / 2f + 50f, table.Height / 2f - 50f)
				}
			};
			return new Trigger(triggerData);
		}

		public void Init(Table.Table table)
		{
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
	}
}
