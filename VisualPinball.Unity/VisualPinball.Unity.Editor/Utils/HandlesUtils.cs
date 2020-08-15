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
