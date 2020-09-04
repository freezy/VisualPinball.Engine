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

using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.Physics
{
	public class HitLine3D : HitLineZ
	{
		public readonly Matrix2D Matrix = new Matrix2D();
		public readonly float ZLow;
		public readonly float ZHigh;

		public readonly Vertex3D V1;
		public readonly Vertex3D V2;

		public HitLine3D(Vertex3D v1, Vertex3D v2, ItemType itemType, IItem item) : base(new Vertex2D(), itemType, item)
		{
			var vLine = v2.Clone().Sub(v1);
			vLine.Normalize();

			// Axis of rotation to make 3D cylinder a cylinder along the z-axis
			var transAxis = new Vertex3D(vLine.Y, -vLine.X, 0);
			var l = transAxis.LengthSq();
			if (l <= 1e-6) {
				// line already points in z axis?
				transAxis.Set(1, 0, 0); // choose arbitrary rotation vector

			} else {
				transAxis.DivideScalar(MathF.Sqrt(l));
			}

			// Angle to rotate the line into the z-axis
			var dot = vLine.Z; //vLine.Dot(&vup);

			Matrix.RotationAroundAxis(transAxis, -MathF.Sqrt(1 - dot * dot), dot);

			var vTrans1 = v1.Clone().ApplyMatrix2D(Matrix);
			var vTrans2 = v2.Clone().ApplyMatrix2D(Matrix);
			var vTrans2Z = vTrans2.Z;

			// set up HitLineZ parameters
			Xy.Set(vTrans1.X, vTrans1.Y);
			ZLow = MathF.Min(vTrans1.Z, vTrans2Z);
			ZHigh = MathF.Max(vTrans1.Z, vTrans2Z);

			V1 = v1;
			V2 = v2;

			HitBBox.Left = MathF.Min(v1.X, v2.X);
			HitBBox.Right = MathF.Max(v1.X, v2.X);
			HitBBox.Top = MathF.Min(v1.Y, v2.Y);
			HitBBox.Bottom = MathF.Max(v1.Y, v2.Y);
			HitBBox.ZLow = MathF.Min(v1.Z, v2.Z);
			HitBBox.ZHigh = MathF.Max(v1.Z, v2.Z);
		}

		public override void CalcHitBBox() {
			// already one in constructor
		}
	}
}
