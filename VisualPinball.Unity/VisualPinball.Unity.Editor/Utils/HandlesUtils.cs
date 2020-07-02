﻿using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.VPT;

namespace VisualPinball.Unity.Editor.Utils
{
	public static class HandlesUtils
	{
		public static Vector3 HandlePosition(Vector3 position, ItemDataTransformType type, Quaternion rotation)
		{
			return HandlePosition(position, type, rotation, 0.2f, 0.0f);
		}

		public static Vector3 HandlePosition(Vector3 position, ItemDataTransformType type, Quaternion rotation, float handleSize, float snap)
		{
			Vector3 forward = rotation * Vector3.forward;
			Vector3 right = rotation * Vector3.right;
			Vector3 up = rotation * Vector3.up;
			Vector3 newPos = position;

			switch (type)
			{
				case ItemDataTransformType.TwoD:
					{
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
				case ItemDataTransformType.ThreeD:
					{
						newPos = Handles.PositionHandle(newPos, rotation);
						break;
					}
				default:
					break;
			}

			return newPos;
		}

	}
}
