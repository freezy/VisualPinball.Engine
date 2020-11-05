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
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.HitTarget
{
	public class HitTarget : Item<HitTargetData>, IRenderable, IHittable, ISwitchable
	{
		public override string ItemName { get; } = "Target";
		public override string ItemGroupName { get; } = "Targets";
		public override ItemType ItemType { get; } = ItemType.HitTarget;

		public Vertex3D Position { get => Data.Position; set => Data.Position = value; }
		public float RotationY { get => Data.RotZ; set => Data.RotZ = value; }

		public bool IsPulseSwitch => true;

		public HitObject[] GetHitShapes() => _hits;

		public readonly HitTargetMeshGenerator MeshGenerator;
		private readonly HitTargetHitGenerator _hitGenerator;
		private HitObject[] _hits;

		public HitTarget(HitTargetData data) : base(data)
		{
			MeshGenerator = new HitTargetMeshGenerator(Data);
			_hitGenerator = new HitTargetHitGenerator(Data, MeshGenerator);
		}

		public HitTarget(BinaryReader reader, string itemName) : this(new HitTargetData(reader, itemName))
		{
		}

		public static HitTarget GetDefault(Table.Table table)
		{
			var hitTargetData = new HitTargetData(table.GetNewName<HitTarget>("Target"), table.Width / 2f, table.Height / 2f);
			return new HitTarget(hitTargetData);
		}

		public void Init(Table.Table table)
		{
			_hits = _hitGenerator.GenerateHitObjects(table, this);
		}

		#region IRenderable

		Matrix3D IRenderable.TransformationMatrix(Table.Table table, Origin origin) => MeshGenerator.GetPostMatrix(table, origin);

		public RenderObject GetRenderObject(Table.Table table, string id = null, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return MeshGenerator.GetRenderObject(table, origin, asRightHanded);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return MeshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		#endregion

		public bool IsCollidable => Data.IsCollidable;
	}
}
