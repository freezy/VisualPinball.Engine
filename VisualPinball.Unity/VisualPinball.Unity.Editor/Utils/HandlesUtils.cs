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

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public static class HandlesUtils
	{
		public static Vector3 HandlePosition(Vector3 position, ItemDataTransformType type, Quaternion rotation)
		{
			return HandlePosition(position, type, rotation, 0.2f, 0.0f);
		}

		private static Vector3 HandlePosition(Vector3 position, ItemDataTransformType type, Quaternion rotation, float handleSize, float snap)
		{
			var forward = rotation * Vector3.forward;
			var right = rotation * Vector3.right;
			var up = rotation * Vector3.up;
			var newPos = position;

			switch (type) {
				case ItemDataTransformType.TwoD: {

					Handles.color = Handles.xAxisColor;
					newPos = Handles.Slider(newPos, right);

					Handles.color = Handles.yAxisColor;
					newPos = Handles.Slider(newPos, up);

					Handles.color = Handles.zAxisColor;
					newPos = Handles.Slider2D(
						newPos,
						forward,
						right,
						up,
						HandleUtility.GetHandleSize(position) * handleSize,
						Handles.RectangleHandleCap,
						snap);
					break;
				}

				case ItemDataTransformType.ThreeD: {
					newPos = Handles.PositionHandle(newPos, rotation);
					break;
				}
			}

			return newPos;
		}

	}
}
