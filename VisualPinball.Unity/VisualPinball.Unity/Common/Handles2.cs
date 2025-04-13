// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity
{
	public static class Handles2
	{
		public static void DrawArrow(Vector3 from, Vector3 to, float width = 1f, float arrowHeadLength = 0.025f, float arrowHeadAngle = 20.0f, bool bothSides = false)
		{
			var direction = to - from;
			var right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * new Vector3(0, 0, 1);
			var left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * new Vector3(0, 0, 1);
			Handles.DrawAAPolyLine(width, from, to);
			Handles.DrawAAPolyLine(width, to + right * arrowHeadLength, to, to + left * arrowHeadLength);
			if (bothSides) {
				Handles.DrawAAPolyLine(width, from - right * arrowHeadLength, from, from - left * arrowHeadLength);
			}
		}
	}
}

#endif
