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

namespace VisualPinball.Engine.VPT.Bumper
{
	public class Bumper : Item<BumperData>, IRenderable, IHittable
	{
		private readonly BumperMeshGenerator _meshGenerator;

		private HitObject[] _hits;

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

		public void Init(Table.Table table)
		{
			var height = table.GetSurfaceHeight(Data.Surface, Data.Center.X, Data.Center.Y);
			_hits = new HitObject[] {new BumperHit(Data, height, this)};
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		public HitObject[] GetHitShapes() => _hits;
	}
}
