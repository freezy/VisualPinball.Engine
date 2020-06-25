using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.VPT;

namespace VisualPinball.Unity.Editor.Utils
{
	class HandlesUtils
	{
		public delegate void OnPositionChange(Vector3 newPosition, params object[] plist);

		public static void HandlePosition(Vector3 position, ItemDataTransformType type, Quaternion rotation, OnPositionChange onChange, params object[] plist)
		{
			HandlePosition(position, type, rotation, 0.2f, 0.0f, onChange, plist);
		}

		public static void HandlePosition(Vector3 position, ItemDataTransformType type, Quaternion rotation, float handleSize, float snap, OnPositionChange onChange, params object[] plist)
		{
			Vector3 handlePos = position;
			Vector3 forward = rotation * Vector3.forward;
			Vector3 right = rotation * Vector3.right;
			Vector3 up = rotation * Vector3.up;

			switch (type)
			{
				case ItemDataTransformType.TwoD:
					{
						EditorGUI.BeginChangeCheck();
						Handles.color = Handles.xAxisColor;
						var newPos = Handles.Slider(handlePos, right);
						if (EditorGUI.EndChangeCheck())
						{
							onChange(newPos, plist);
						}

						EditorGUI.BeginChangeCheck();
						Handles.color = Handles.yAxisColor;
						newPos = Handles.Slider(handlePos, up);
						if (EditorGUI.EndChangeCheck())
						{
							onChange(newPos, plist);
						}

						EditorGUI.BeginChangeCheck();
						Handles.color = Handles.zAxisColor;
						newPos = Handles.Slider2D(
							handlePos,
							forward,
							right,
							up,
							HandleUtility.GetHandleSize(handlePos) * handleSize,
							Handles.RectangleHandleCap,
							snap);
						if (EditorGUI.EndChangeCheck())
						{
							onChange(newPos, plist);
						}
						break;
					}
				case ItemDataTransformType.ThreeD:
					{
						EditorGUI.BeginChangeCheck();
						Vector3 newPos = Handles.PositionHandle(handlePos, rotation);
						if (EditorGUI.EndChangeCheck())
						{
							onChange(newPos, plist);
						}
						break;
					}
				default:
					break;
			}
		}

	}
}
