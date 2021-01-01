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

namespace VisualPinball.Engine.VPT.Surface
{
	public class Surface : Item<SurfaceData>, IRenderable, IHittable
	{
		public override string ItemName { get; } = "Wall";
		public override string ItemGroupName { get; } = "Walls";

		public Vertex3D Position { get => new Vertex3D(0, 0, 0); set { } }
		public float RotationY { get => 0; set { } }

		public HitObject[] GetHitShapes() => _hits;
		public bool IsCollidable => Data.IsCollidable;

		private readonly SurfaceMeshGenerator _meshGenerator;
		private readonly SurfaceHitGenerator _hitGenerator;
		private HitObject[] _hits;

		public Surface(SurfaceData data) : base(data)
		{
			_meshGenerator = new SurfaceMeshGenerator(Data);
			_hitGenerator = new SurfaceHitGenerator(Data);
		}

		public Surface(BinaryReader reader, string itemName) : this(new SurfaceData(reader, itemName))
		{
		}

		public static Surface GetDefault(Table.Table table)
		{
			var surfaceData = new SurfaceData(
				table.GetNewName<Surface>("Wall"),
				new[] {
					new DragPointData(table.Width / 2f - 50f, table.Height / 2f - 50f),
					new DragPointData(table.Width / 2f - 50f, table.Height / 2f + 50f),
					new DragPointData(table.Width / 2f + 50f, table.Height / 2f + 50f),
					new DragPointData(table.Width / 2f + 50f, table.Height / 2f - 50f)
				}
			);
			return new Surface(surfaceData);
		}

		public void Init(Table.Table table)
		{
			_hits = _hitGenerator.GenerateHitObjects(table, this);
		}

		#region IRenderable

		Matrix3D IRenderable.TransformationMatrix(Table.Table table, Origin origin) => Matrix3D.Identity;

		public RenderObject GetRenderObject(Table.Table table, string id = null, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObject(table, id, asRightHanded);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, asRightHanded);
		}

		#endregion
	}
}
