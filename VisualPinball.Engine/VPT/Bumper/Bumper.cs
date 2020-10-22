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

using System;
using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Bumper
{
	public class Bumper : Item<BumperData>, IRenderable, IHittable, ISwitchable, ICoilable
	{
		public override string ItemName { get; } = "Bumper";
		public override string ItemGroupName { get; } = "Bumpers";

		public Vertex3D Position { get => new Vertex3D(Data.Center.X, Data.Center.Y, 0); set => Data.Center = new Vertex2D(value.X, value.Y); }
		public float RotationY { get => Data.Orientation; set => Data.Orientation = value; }

		public bool IsPulseSwitch => true;

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

		#region IRenderable

		Matrix3D IRenderable.TransformationMatrix(Table.Table table, Origin origin)
		{
			switch (origin) {
				case Origin.Original:
					var rotMatrix = new Matrix3D().RotateZMatrix(MathF.DegToRad(Data.Orientation));
					var transMatrix = new Matrix3D().SetTranslation(Data.Center.X, Data.Center.Y, 0f);
					return rotMatrix.Multiply(transMatrix);

				case Origin.Global:
					return Matrix3D.Identity;

				default:
					throw new ArgumentOutOfRangeException(nameof(origin), origin, "Unknown origin " + origin);
			}
		}

		public RenderObject GetRenderObject(Table.Table table, string id = null, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObject(table, id, origin, asRightHanded);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		#endregion

		public HitObject[] GetHitShapes() => _hits;
		public bool IsCollidable => Data.IsCollidable;
		public bool IsDualWound { get; set; }
	}
}
