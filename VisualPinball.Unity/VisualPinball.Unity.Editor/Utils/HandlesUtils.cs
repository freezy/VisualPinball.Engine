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

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public static class HandlesUtils
	{
		/// <summary>
		/// Returns the position of the moved object in VPX space.
		/// </summary>
		/// <param name="position">Original position in VPX space</param>
		/// <param name="localToWorld">The local-to-world matrix of the item</param>
		/// <param name="type">Allowed position type</param>
		/// <param name="handleSize"></param>
		/// <param name="snap"></param>
		/// <returns>Moved position in VPX space.</returns>
		public static Vector3 HandlePosition(Vector3 position, Matrix4x4 localToWorld, DragPointTransformType type, float handleSize = 0.2f, float snap = 0.0f)
		{

			var pos = position.TranslateToWorld();
			Handles.matrix = localToWorld;

			switch (type) {
				case DragPointTransformType.TwoD: {

					var forward = Vector3.forward.TranslateToWorld().normalized;
					var right = Vector3.right.TranslateToWorld().normalized;
					var up = Vector3.up.TranslateToWorld().normalized;

					Handles.color = Handles.xAxisColor;
					pos = Handles.Slider(pos, right);

					Handles.color = Handles.yAxisColor;
					pos = Handles.Slider(pos, up);

					Handles.color = Handles.zAxisColor;
					pos = Handles.Slider2D(
						pos,
						forward,
						right,
						up,
						HandleUtility.GetHandleSize(pos) * handleSize,
						Handles.RectangleHandleCap,
						snap);
					break;
				}

				case DragPointTransformType.ThreeD: {
					pos = Handles.PositionHandle(pos, Quaternion.identity.RotateToWorld());
					break;
				}
			}
			return pos.TranslateToVpx();
		}

	}
}
