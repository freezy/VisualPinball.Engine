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

using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public static class CurveUtils
	{
		// awful linear interpolations : TODO: replace by AnimationCurve equivalent...
		public static float PSlope(float x, float x1, float y1, float x2, float y2)  //Set up line via two points, no clamping. Input X, output Y
		{
			float m = (y2 - y1) / (y2 - x1);
			float b = y2 - m * y2;
			return m * x + b;
		}

		public static float LinearEnvelope(float xInput, ref BlobArray<float2> curve, float defaultValue = 1F)
		{
			if (curve.Length <= 0)
				return defaultValue;

			if (xInput <= curve[0].x) //Clamp lower
				return curve[0].y;
			if (xInput >= curve[curve.Length - 1].x) //Clamp upper
				return curve[curve.Length - 1].y;

			int L = -1;
			for (int ii = 1; ii < curve.Length; ii++)    //find active line
				if (xInput <= curve[ii].x)
				{
					L = ii;
					break;
				}

			if (L < 0)
				return defaultValue;

			if (xInput > curve[curve.Length - 1].x) // catch line overrun
				L = curve.Length - 1;

			float y = PSlope(xInput, curve[L - 1].x, curve[L - 1].y, curve[L].x, curve[L - 1].y);

			return y;
		}

		// if segments are even on x axis, no need to iterate: faster
		public static float LinearEnvelopeEven(float xInput, ref BlobArray<float2> curve, float defaultValue = 1F)
		{
			if (curve.Length <= 0)
				return defaultValue;

			if (xInput <= curve[0].x) //Clamp lower
				return curve[0].y;
			if (xInput >= curve[curve.Length - 1].x) //Clamp upper
				return curve[curve.Length - 1].y;

			int L = (int)((xInput - curve[0].x) * (curve.Length - 1) / (curve[curve.Length - 1].x - curve[0].x));

			float y = PSlope(xInput, curve[L - 1].x, curve[L - 1].y, curve[L].x, curve[L - 1].y);

			return y;
		}

		/// <summary>
		/// Slices the animation in nb parts. Dest should be preallocated array using nb+1.
		/// </summary>
		/// <param name="curve">Animation curve to slice</param>
		/// <param name="dest">Preallocate (nb+1) blob array</param>
		/// <param name="nb">Number of sliced segments</param>
		/// <param name="defaultValue">Default value if curve is null</param>
		public static void CurveToBlobArray(ref UnityEngine.AnimationCurve curve, ref BlobBuilderArray<float2> dest, int nb, float defaultValue = 0F)
		{
			if (curve != null)
			{
				float stepP = (curve[curve.length - 1].time - curve[0].time) / nb;
				int i = 0;
				for (var t = curve[0].time; t <= curve[curve.length - 1].time; t += stepP)
				{
					dest[i].x = t;
					dest[i++].y = curve.Evaluate(t);
				}
			}
			else
			{
				for (int i = 0; i < nb + 1; i++)
				{
					dest[i].x = i / (float)nb;
					dest[i].y = defaultValue;
				}
			}
		}
	}
}
