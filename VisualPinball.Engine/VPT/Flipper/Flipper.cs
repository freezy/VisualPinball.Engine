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

namespace VisualPinball.Engine.VPT.Flipper
{
	public class Flipper : Item<FlipperData>, IRenderable, IHittable
	{
		private readonly FlipperMeshGenerator _meshGenerator;
		private FlipperHit _hit;

		public Flipper(FlipperData data) : base(data)
		{
			_meshGenerator = new FlipperMeshGenerator(Data);
		}

		public Flipper(BinaryReader reader, string itemName) : this(new FlipperData(reader, itemName))
		{
		}

		public static Flipper GetDefault(Table.Table table)
		{
			var flipperData = new FlipperData(table.GetNewName<Flipper>("Flipper"), table.Width / 2f, table.Height / 2f);
			return new Flipper(flipperData);
		}

		public void Init(Table.Table table)
		{
			_hit = new FlipperHit(Data, table, this);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		public HitObject[] GetHitShapes() => new HitObject[] { _hit };
	}
}
