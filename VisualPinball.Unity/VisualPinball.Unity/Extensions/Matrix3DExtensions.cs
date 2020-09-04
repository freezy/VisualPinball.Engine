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

using UnityEngine;

namespace VisualPinball.Unity
{
	public static class Matrix3DExtensions
	{
		public static Matrix4x4 ToUnityMatrix(this Engine.Math.Matrix3D vpMatrix)
		{
			var c1 = vpMatrix.Column1;
			var c2 = vpMatrix.Column2;
			var c3 = vpMatrix.Column3;
			var c4 = vpMatrix.Column4;
			return new Matrix4x4(
				new Vector4(c1.Item1, c1.Item2, c1.Item3, c1.Item4),
				new Vector4(c2.Item1, c2.Item2, c2.Item3, c2.Item4),
				new Vector4(c3.Item1, c3.Item2, c3.Item3, c3.Item4),
				new Vector4(c4.Item1, c4.Item2, c4.Item3, c4.Item4)
			);
		}
	}
}
