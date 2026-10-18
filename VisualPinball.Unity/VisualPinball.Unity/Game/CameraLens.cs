// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The vertical field of view a camera shows on screen, for plain and physical cameras alike. A physical
	/// camera's <see cref="Camera.fieldOfView"/> comes from the sensor's height only, while its gate fit can
	/// fit the sensor's width to the screen instead; this reads and sets what the picture really spans.
	/// </summary>
	///
	/// <remarks>
	/// Works on the logical picture: for an output turned by <see cref="DisplayRotation"/>, its aspect is the
	/// turned one, and the camera is the unturned one the scripts see between frames.
	/// </remarks>
	public static class CameraLens
	{
		public const float MinVerticalFov = 3f;
		public const float MaxVerticalFov = 100f;

		/// <summary>Width over height of the picture the camera renders, turned when the output is.</summary>
		public static float LogicalAspect(Camera camera)
		{
			var size = DisplayRotation.LogicalSize(camera.pixelWidth, camera.pixelHeight);
			return size.y > 0 ? size.x / (float)size.y : 1f;
		}

		/// <summary>The vertical field of view in degrees.</summary>
		public static float GetVerticalFov(Camera camera)
		{
			if (!camera.usePhysicalProperties) {
				return camera.fieldOfView;
			}
			var sensor = camera.sensorSize;
			var focal = math.max(camera.focalLength, 0.01f);
			var tanHalf = FitsWidth(camera, sensor)
				? sensor.x * 0.5f / focal / LogicalAspect(camera)
				: sensor.y * 0.5f / focal;
			return 2f * math.degrees(math.atan(tanHalf));
		}

		/// <summary>Sets the vertical field of view in degrees; a physical camera changes its focal length.</summary>
		public static void SetVerticalFov(Camera camera, float degrees)
		{
			degrees = math.clamp(degrees, MinVerticalFov, MaxVerticalFov);
			if (!camera.usePhysicalProperties) {
				camera.fieldOfView = degrees;
				return;
			}
			var sensor = camera.sensorSize;
			var tanHalf = math.tan(math.radians(degrees) * 0.5f);
			camera.focalLength = FitsWidth(camera, sensor)
				? sensor.x * 0.5f / (tanHalf * LogicalAspect(camera))
				: sensor.y * 0.5f / tanHalf;
		}

		// Whether the sensor's width spans the picture's width (else its height spans the picture's height).
		private static bool FitsWidth(Camera camera, Vector2 sensor)
		{
			var sensorAspect = sensor.y > 0f ? sensor.x / sensor.y : 1f;
			var aspect = LogicalAspect(camera);
			return camera.gateFit switch {
				Camera.GateFitMode.Horizontal => true,
				Camera.GateFitMode.Fill => aspect > sensorAspect,
				Camera.GateFitMode.Overscan => aspect < sensorAspect,
				_ => false,
			};
		}
	}
}
