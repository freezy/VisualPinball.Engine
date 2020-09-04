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

namespace VisualPinball.Engine.VPT.Plunger
{
	public class Plunger : Item<PlungerData>, IRenderable, IHittable
	{
		public PlungerHit PlungerHit { get; private set; }

		public const float PlungerHeight = 50.0f;
		public const float PlungerMass = 30.0f;
		public const int PlungerNormalize = 100;

		public readonly PlungerMeshGenerator MeshGenerator;

		private HitObject[] _hitObjects;

		public Plunger(PlungerData data) : base(data)
		{
			MeshGenerator = new PlungerMeshGenerator(data);
		}

		public Plunger(BinaryReader reader, string itemName) : this(new PlungerData(reader, itemName))
		{
		}

		public static Plunger GetDefault(Table.Table table)
		{
			var plungerData = new PlungerData(table.GetNewName<Plunger>("Plunger"), table.Width / 2f, table.Height / 2f);
			return new Plunger(plungerData);
		}

		public void Init(Table.Table table)
		{
			var zHeight = table.GetSurfaceHeight(Data.Surface, Data.Center.X, Data.Center.Y);
			PlungerHit = new PlungerHit(Data, zHeight, this);
			_hitObjects = new HitObject[] { PlungerHit };
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return MeshGenerator.GetRenderObjects(20, table, origin, asRightHanded);
		}

		public HitObject[] GetHitShapes() => _hitObjects;
	}
}
