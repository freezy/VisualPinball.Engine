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
		/// <param name="playfield">Reference to parent playfield</param>
		/// <param name="position">Original position in VPX space</param>
		/// <param name="type">Allowed position type</param>
		/// <returns>Moved position in VPX space.</returns>
		public static Vector3 HandlePosition(PlayfieldComponent playfield, Vector3 position, ItemDataTransformType type)
		{
			return HandlePosition(playfield, position, type, 0.2f, 0.0f);
		}

		private static Vector3 HandlePosition(PlayfieldComponent playfield, Vector3 position, ItemDataTransformType type, float handleSize, float snap)
		{
			var forward = Vector3.forward.TranslateToWorld();
			var right = Vector3.right.TranslateToWorld();
			var up = Vector3.up.TranslateToWorld();
			var newPos = position.TranslateToWorld();
			Handles.matrix = playfield == null ? Matrix4x4.identity : playfield.transform.localToWorldMatrix;
			
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
						HandleUtility.GetHandleSize(newPos) * handleSize,
						Handles.RectangleHandleCap,
						snap);
					break;
				}

				case ItemDataTransformType.ThreeD: {
					newPos = Handles.PositionHandle(newPos, Quaternion.identity.RotateToWorld());
					break;
				}
			}
			return newPos.TranslateToVpx();
		}

	}
}
