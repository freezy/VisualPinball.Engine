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
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT
{
	public abstract class MeshGenerator
	{
		protected abstract float BaseHeight(Table.Table table);
		protected abstract Vertex3D Position { get; }
		protected abstract Vertex3D Scale { get; }
		protected abstract float RotationZ { get; }

		protected Tuple<Matrix3D, Matrix3D> GetPreMatrix(Table.Table table, Origin origin, bool asRightHanded)
		{
			// todo adjust position, see kicker.cpp#419+
			switch (origin) {
				case Origin.Original:
					return asRightHanded
						? new Tuple<Matrix3D, Matrix3D>(Matrix3D.RightHanded, null)
						: new Tuple<Matrix3D, Matrix3D>(Matrix3D.Identity, null);

				case Origin.Global:
					var m = GetTransformationMatrix(table);
					return asRightHanded
						? new Tuple<Matrix3D, Matrix3D>(m.Item1.Multiply(Matrix3D.RightHanded), m.Item2?.Multiply(Matrix3D.RightHanded))
						: m;
				default:
					throw new ArgumentOutOfRangeException(nameof(origin), origin, "Unknown origin " + origin);
			}
		}

		protected Matrix3D GetPostMatrix(Table.Table table, Origin origin)
		{
			switch (origin) {
				case Origin.Original: return GetTransformationMatrix(table).Item1;
				case Origin.Global: return Matrix3D.Identity;
				default:
					throw new ArgumentOutOfRangeException(nameof(origin), origin, "Unknown origin " + origin);
			}
		}

		protected virtual Tuple<Matrix3D, Matrix3D> GetTransformationMatrix(Table.Table table)
		{
			var scale = Scale;
			var position = Position;

			// scale matrix
			var scaleMatrix = new Matrix3D();
			scaleMatrix.SetScaling(scale.X, scale.Y, scale.Z);

			// translation matrix
			var transMatrix = new Matrix3D();
			transMatrix.SetTranslation(position.X, position.Y, position.Z + BaseHeight(table));

			// rotation matrix
			var rotMatrix = new Matrix3D();
			rotMatrix.RotateZMatrix(RotationZ);

			var fullMatrix = scaleMatrix.Clone();
			fullMatrix.Multiply(rotMatrix);
			fullMatrix.Multiply(transMatrix);
			scaleMatrix.SetScaling(1.0f, 1.0f, table.GetScaleZ());
			fullMatrix.Multiply(scaleMatrix);

			return new Tuple<Matrix3D, Matrix3D>(fullMatrix, null);
		}
	}
}
