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

namespace VisualPinball.Engine.VPT.Plunger
{
	public class Plunger : Item<PlungerData>, IRenderable, IHittable, ICoilable
	{
		public override string ItemName { get; } = "Plunger";
		public override string ItemGroupName { get; } = "Plungers";

		public Vertex3D Position { get => new Vertex3D(Data.Center.X, Data.Center.Y, 0); set => Data.Center = new Vertex2D(value.X, value.Y); }
		public float RotationY { get => 0; set { } }

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

		#region IRenderable

		Matrix3D IRenderable.TransformationMatrix(Table.Table table, Origin origin) => Matrix3D.Identity;

		public RenderObject GetRenderObject(Table.Table table, string id = null, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return MeshGenerator.GetRenderObject(20, table, id, origin, asRightHanded);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return MeshGenerator.GetRenderObjects(20, table, origin, asRightHanded);
		}

		#endregion

		public HitObject[] GetHitShapes() => _hitObjects;
		public bool IsCollidable => true;

		public bool IsDualWound { get; set; }
	}
}
