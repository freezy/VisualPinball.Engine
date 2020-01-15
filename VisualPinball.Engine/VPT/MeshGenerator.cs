using System;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT
{
	public abstract class MeshGenerator
	{
		protected abstract Tuple<Matrix3D, Matrix3D> GetTransformationMatrix(Table.Table table);

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
	}
}
