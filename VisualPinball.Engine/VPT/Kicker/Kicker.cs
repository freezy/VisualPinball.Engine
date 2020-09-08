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
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Kicker
{
	public class Kicker : Item<KickerData>, IRenderable, IBallCreationPosition, IHittable
	{
		public KickerHit KickerHit => _hit;
		public string[] UsedMaterials => new[] { Data.Material };

		private readonly KickerMeshGenerator _meshGenerator;
		private KickerHit _hit;

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


		public void Init(Table.Table table)
		{
			var height = table.GetSurfaceHeight(Data.Surface, Data.Center.X, Data.Center.Y) * table.GetScaleZ();

			// reduce the hit circle radius because only the inner circle of the kicker should start a hit event
			var radius = Data.Radius * (Data.LegacyMode ? Data.FallThrough ? 0.75f : 0.6f : 1f);
			_hit = new KickerHit(Data, radius, height, table, this); // height of kicker hit cylinder
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		public HitObject[] GetHitShapes()
		{
			return new HitObject[] {_hit};
		}

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
