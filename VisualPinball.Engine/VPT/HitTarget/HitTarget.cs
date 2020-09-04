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
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.HitTarget
{
	public class HitTarget : Item<HitTargetData>, IRenderable, IHittable
	{
		public HitObject[] GetHitShapes() => _hits;

		private readonly HitTargetMeshGenerator _meshGenerator;
		private readonly HitTargetHitGenerator _hitGenerator;
		private HitObject[] _hits;

		public HitTarget(HitTargetData data) : base(data)
		{
			_meshGenerator = new HitTargetMeshGenerator(Data);
			_hitGenerator = new HitTargetHitGenerator(Data, _meshGenerator);
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

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
