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
