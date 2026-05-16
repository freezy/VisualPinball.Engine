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

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public interface IPackageScreenshotEnvironmentProvider
	{
		IDisposable CreateEnvironmentScope(Transform tableRoot, Cubemap hdriCubemap, float hdriExposure);
	}

	public static class PackageScreenshotEnvironmentProvider
	{
		private static IPackageScreenshotEnvironmentProvider _active;

		public static void Register(IPackageScreenshotEnvironmentProvider provider)
		{
			_active = provider;
		}

		public static IDisposable CreateScope(Transform tableRoot, Cubemap hdriCubemap, float hdriExposure)
		{
			return _active?.CreateEnvironmentScope(tableRoot, hdriCubemap, hdriExposure);
		}
	}

	internal static class PackageScreenshotGenerator
	{
		internal const int PortraitWidth = 2160;
		internal const int PortraitHeight = 3840;
		internal const float FieldOfView = 3f;
		internal const string DefaultHdriAssetPath = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/Assets/studio_small_08_1k.exr";

		private static readonly Quaternion TopDownRotation = Quaternion.Euler(90f, 0f, 0f);

		private const float MarginFactor = 1.05f;
		private const float MinNearPadding = 0.25f;
		private const float MinFarPadding = 0.5f;
		private const byte BackgroundTolerance = 4;

		private static readonly Color Transparent = new(0f, 0f, 0f, 0f);

		internal static string GetSuggestedFileName(TableComponent tableComponent)
			=> $"{tableComponent.name}-package-preview";

		internal static PackageScreenshotResult Generate(TableComponent tableComponent, string assetPath, Cubemap hdriCubemap, float hdriExposure)
		{
			if (!tableComponent) {
				throw new ArgumentNullException(nameof(tableComponent));
			}

			if (string.IsNullOrWhiteSpace(assetPath)) {
				throw new ArgumentException("A valid asset path is required.", nameof(assetPath));
			}

			if (!hdriCubemap) {
				throw new ArgumentNullException(nameof(hdriCubemap), "A HDRI cubemap is required.");
			}

			var playfield = tableComponent.GetComponentInChildren<PlayfieldComponent>(true);
			if (!playfield) {
				throw new InvalidOperationException("No PlayfieldComponent was found under the selected table.");
			}

			var playfieldBounds = GetPlayfieldBounds(playfield);
			var tableBounds = tableComponent.GetTableBounds();
			var absolutePath = GetAbsolutePath(assetPath);
			var referenceCamera = FindReferenceCamera(tableComponent);

			Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

			RenderTexture beautyRenderTexture = null;
			RenderTexture alphaRenderTexture = null;
			Texture2D beautyTexture = null;
			Texture2D alphaTexture = null;
			var previousActive = RenderTexture.active;
			var backgroundKeyColor = GetBackgroundKeyColor(referenceCamera);

			try {
				var playfieldCenter = playfieldBounds.center;
				var playfieldWidth = playfieldBounds.size.x;
				var playfieldHeight = playfieldBounds.size.z;
				var referenceDistance = Mathf.Abs(referenceCamera.transform.position.y - playfieldCenter.y);
				beautyRenderTexture = CreateRenderTexture("Package Screenshot Beauty");
				alphaRenderTexture = CreateRenderTexture("Package Screenshot Alpha");
				beautyTexture = new Texture2D(PortraitWidth, PortraitHeight, TextureFormat.RGBA32, false);
				alphaTexture = new Texture2D(PortraitWidth, PortraitHeight, TextureFormat.RGBA32, false);

				var cameraState = new CameraCaptureState(referenceCamera);
				try {
					cameraState.ApplyForBeautyCapture(beautyRenderTexture);
					var cameraDistance = CalculateTopDownDistance(referenceCamera, playfieldWidth, playfieldHeight);
					var cameraPosition = new Vector3(playfieldCenter.x, playfieldCenter.y + cameraDistance, playfieldCenter.z);
					referenceCamera.transform.SetPositionAndRotation(cameraPosition, TopDownRotation);
					var clipPlanes = CalculateClipPlanes(tableBounds, referenceCamera.transform);
					referenceCamera.nearClipPlane = clipPlanes.x;
					referenceCamera.farClipPlane = clipPlanes.y;

					using var environmentScope = PackageScreenshotEnvironmentProvider.CreateScope(tableComponent.transform, hdriCubemap, hdriExposure);
					referenceCamera.Render();
					ReadRenderTexture(beautyRenderTexture, beautyTexture);
					File.WriteAllBytes(absolutePath, beautyTexture.EncodeToPNG());
					AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

					return new PackageScreenshotResult(
						assetPath,
						absolutePath,
						cameraPosition,
						cameraDistance,
						playfieldBounds,
						cameraState.OriginalPosition,
						referenceDistance
					);
				} finally {
					cameraState.Restore();
				}
			} finally {
				RenderTexture.active = previousActive;
				if (beautyRenderTexture) {
					beautyRenderTexture.Release();
				}
				if (alphaRenderTexture) {
					alphaRenderTexture.Release();
				}
				if (beautyTexture) {
					UnityEngine.Object.DestroyImmediate(beautyTexture);
				}
				if (alphaTexture) {
					UnityEngine.Object.DestroyImmediate(alphaTexture);
				}
				if (beautyRenderTexture) {
					UnityEngine.Object.DestroyImmediate(beautyRenderTexture);
				}
				if (alphaRenderTexture) {
					UnityEngine.Object.DestroyImmediate(alphaRenderTexture);
				}
			}
		}

		private static RenderTexture CreateRenderTexture(string name)
		{
			var renderTexture = new RenderTexture(PortraitWidth, PortraitHeight, 24, RenderTextureFormat.ARGB32) {
				antiAliasing = 1,
				name = name,
			};
			renderTexture.Create();
			return renderTexture;
		}

		private static void ReadRenderTexture(RenderTexture renderTexture, Texture2D texture)
		{
			RenderTexture.active = renderTexture;
			texture.ReadPixels(new Rect(0f, 0f, PortraitWidth, PortraitHeight), 0, 0, false);
			texture.Apply(false, false);
		}

		private static Bounds GetPlayfieldBounds(PlayfieldComponent playfield)
		{
			var width = Mathf.Abs(Physics.ScaleToWorld(playfield.Right - playfield.Left));
			var height = Mathf.Abs(Physics.ScaleToWorld(playfield.Bottom - playfield.Top));
			var centerLocal = new Vector3(
				Physics.ScaleToWorld((playfield.Left + playfield.Right) * 0.5f),
				0f,
				-Physics.ScaleToWorld((playfield.Top + playfield.Bottom) * 0.5f)
			);
			var centerWorld = playfield.transform.TransformPoint(centerLocal);
			var size = Vector3.Scale(new Vector3(width, 0.01f, height), Abs(playfield.transform.lossyScale));
			return new Bounds(centerWorld, size);
		}

		private static float CalculateTopDownDistance(Camera camera, float playfieldWidth, float playfieldHeight)
		{
			if (!camera || camera.aspect <= 0f) {
				throw new InvalidOperationException("Unable to calculate the screenshot camera distance from the configured FOV.");
			}

			var verticalFieldOfView = GetEffectiveVerticalFieldOfView(camera);
			var verticalHalfAngleTangent = Mathf.Tan(verticalFieldOfView * 0.5f * Mathf.Deg2Rad);
			var horizontalFieldOfView = Camera.VerticalToHorizontalFieldOfView(verticalFieldOfView, camera.aspect);
			var horizontalHalfAngleTangent = Mathf.Tan(horizontalFieldOfView * 0.5f * Mathf.Deg2Rad);
			if (verticalHalfAngleTangent <= 0f || horizontalHalfAngleTangent <= 0f) {
				throw new InvalidOperationException("Unable to calculate the screenshot camera distance from the configured FOV.");
			}

			var widthWithMargin = playfieldWidth * MarginFactor;
			var heightWithMargin = playfieldHeight * MarginFactor;
			var distanceForHeight = (heightWithMargin * 0.5f) / verticalHalfAngleTangent;
			var distanceForWidth = (widthWithMargin * 0.5f) / horizontalHalfAngleTangent;
			return Mathf.Max(distanceForHeight, distanceForWidth);
		}

		private static float GetEffectiveVerticalFieldOfView(Camera camera)
		{
			return camera.usePhysicalProperties ? camera.GetGateFittedFieldOfView() : camera.fieldOfView;
		}

		private static Vector3[] GetBoundsCorners(Bounds bounds)
		{
			var min = bounds.min;
			var max = bounds.max;

			return new[] {
				new Vector3(min.x, min.y, min.z),
				new Vector3(min.x, min.y, max.z),
				new Vector3(min.x, max.y, min.z),
				new Vector3(min.x, max.y, max.z),
				new Vector3(max.x, min.y, min.z),
				new Vector3(max.x, min.y, max.z),
				new Vector3(max.x, max.y, min.z),
				new Vector3(max.x, max.y, max.z),
			};
		}

		private static Vector2 CalculateClipPlanes(Bounds bounds, Transform cameraTransform)
		{
			var corners = GetBoundsCorners(bounds);
			var minCameraZ = float.PositiveInfinity;
			var maxCameraZ = float.NegativeInfinity;

			foreach (var corner in corners) {
				var cameraSpacePoint = cameraTransform.InverseTransformPoint(corner);
				minCameraZ = Mathf.Min(minCameraZ, cameraSpacePoint.z);
				maxCameraZ = Mathf.Max(maxCameraZ, cameraSpacePoint.z);
			}

			if (!float.IsFinite(minCameraZ) || !float.IsFinite(maxCameraZ)) {
				return new Vector2(0.01f, 100f);
			}

			var depthSpan = Mathf.Max(0.01f, maxCameraZ - minCameraZ);
			var nearPadding = Mathf.Max(MinNearPadding, depthSpan * 0.1f);
			var farPadding = Mathf.Max(MinFarPadding, depthSpan * 0.15f);
			var nearClip = Mathf.Max(0.01f, minCameraZ - nearPadding);
			var farClip = Mathf.Max(nearClip + 1f, maxCameraZ + farPadding);
			return new Vector2(nearClip, farClip);
		}

		private static string GetAbsolutePath(string assetPath)
		{
			if (Path.IsPathRooted(assetPath)) {
				return assetPath;
			}

			var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
			if (string.IsNullOrEmpty(projectRoot)) {
				throw new InvalidOperationException("Unable to resolve the Unity project root path.");
			}

			return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
		}

		private static Vector3 Abs(Vector3 vector)
			=> new(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));

		private static Camera FindReferenceCamera(TableComponent tableComponent)
		{
			var cameras = UnityEngine.Object.FindObjectsOfType<Camera>(true);
			Camera fallback = null;

			foreach (var candidate in cameras) {
				if (!candidate || candidate.cameraType == CameraType.Preview || candidate.gameObject.hideFlags != HideFlags.None) {
					continue;
				}
				if (!candidate.gameObject.scene.IsValid()) {
					continue;
				}
				if (candidate.CompareTag("MainCamera") && candidate.gameObject.activeInHierarchy) {
					return candidate;
				}
				if (fallback == null && candidate.gameObject.activeInHierarchy) {
					fallback = candidate;
				}
			}

			if (fallback) {
				return fallback;
			}

			throw new InvalidOperationException($"No scene camera was found to copy render settings from for table '{tableComponent.name}'.");
		}

		private sealed class CameraCaptureState
		{
			private readonly Camera _camera;
			private readonly Transform _transform;
			private readonly RenderTexture _originalTargetTexture;
			private readonly CameraClearFlags _originalClearFlags;
			private readonly Color _originalBackgroundColor;
			private readonly float _originalAspect;
			private readonly bool _originalForceIntoRenderTexture;
			private readonly float _originalNearClipPlane;
			private readonly float _originalFarClipPlane;
			private readonly object _hdCamera;
			private readonly PropertyInfo _backgroundColorProperty;
			private readonly object _originalHdrpBackgroundColor;
			private readonly PropertyInfo _clearColorModeProperty;
			private readonly object _originalHdrpClearColorMode;

			public Vector3 OriginalPosition { get; }
			private readonly Quaternion _originalRotation;

			public CameraCaptureState(Camera camera)
			{
				_camera = camera ?? throw new ArgumentNullException(nameof(camera));
				_transform = camera.transform;
				OriginalPosition = _transform.position;
				_originalRotation = _transform.rotation;
				_originalTargetTexture = camera.targetTexture;
				_originalClearFlags = camera.clearFlags;
				_originalBackgroundColor = camera.backgroundColor;
				_originalAspect = camera.aspect;
				_originalForceIntoRenderTexture = camera.forceIntoRenderTexture;
				_originalNearClipPlane = camera.nearClipPlane;
				_originalFarClipPlane = camera.farClipPlane;

				_hdCamera = GetHdAdditionalCameraData(camera.gameObject);
				if (_hdCamera != null) {
					var hdCameraType = _hdCamera.GetType();
					_backgroundColorProperty = hdCameraType.GetProperty("backgroundColorHDR");
					_originalHdrpBackgroundColor = _backgroundColorProperty?.GetValue(_hdCamera);
					_clearColorModeProperty = hdCameraType.GetProperty("clearColorMode");
					_originalHdrpClearColorMode = _clearColorModeProperty?.GetValue(_hdCamera);
				}
			}

			public void ApplyForBeautyCapture(RenderTexture renderTexture)
			{
				_camera.targetTexture = renderTexture;
				_camera.aspect = PortraitWidth / (float)PortraitHeight;
				_camera.forceIntoRenderTexture = true;
			}

			public void ApplyForAlphaCapture(RenderTexture renderTexture, Color backgroundColor)
			{
				ApplyForBeautyCapture(renderTexture);
				_camera.clearFlags = CameraClearFlags.SolidColor;
				_camera.backgroundColor = backgroundColor;
			}

			public void Restore()
			{
				_transform.SetPositionAndRotation(OriginalPosition, _originalRotation);
				_camera.targetTexture = _originalTargetTexture;
				_camera.clearFlags = _originalClearFlags;
				_camera.backgroundColor = _originalBackgroundColor;
				_camera.aspect = _originalAspect;
				_camera.forceIntoRenderTexture = _originalForceIntoRenderTexture;
				_camera.nearClipPlane = _originalNearClipPlane;
				_camera.farClipPlane = _originalFarClipPlane;

				if (_hdCamera == null) {
					return;
				}

				_backgroundColorProperty?.SetValue(_hdCamera, _originalHdrpBackgroundColor);
				_clearColorModeProperty?.SetValue(_hdCamera, _originalHdrpClearColorMode);
			}
		}

		private static void CopyCameraSettings(Camera sourceCamera, GameObject destinationCameraGo, Camera destinationCamera)
		{
			if (!sourceCamera) {
				return;
			}

			destinationCamera.CopyFrom(sourceCamera);
			var listener = destinationCameraGo.GetComponent<AudioListener>();
			if (listener) {
				UnityEngine.Object.DestroyImmediate(listener);
			}

			var sourceHdCamera = GetHdAdditionalCameraData(sourceCamera.gameObject);
			if (sourceHdCamera == null) {
				return;
			}

			var destinationHdCamera = GetHdAdditionalCameraData(destinationCameraGo) ?? destinationCameraGo.AddComponent(sourceHdCamera.GetType());
			var copyToMethod = sourceHdCamera.GetType().GetMethod("CopyTo");
			copyToMethod?.Invoke(sourceHdCamera, new[] { destinationHdCamera });
		}

		private static Color GetBackgroundKeyColor(Camera referenceCamera)
		{
			if (referenceCamera) {
				var color = referenceCamera.backgroundColor;
				color.a = 1f;
				return color;
			}

			return new Color(0.025f, 0.07f, 0.19f, 1f);
		}

		private static void TryConfigureHdrpCamera(GameObject cameraGo, Color backgroundColor)
		{
			var hdCameraType = Type.GetType("UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData, Unity.RenderPipelines.HighDefinition.Runtime");
			if (hdCameraType == null) {
				return;
			}

			var hdCamera = cameraGo.GetComponent(hdCameraType) ?? cameraGo.AddComponent(hdCameraType);
			SetEnumProperty(hdCameraType, hdCamera, "clearColorMode", "Color");

			var backgroundProperty = hdCameraType.GetProperty("backgroundColorHDR");
			backgroundProperty?.SetValue(hdCamera, backgroundColor);
		}

		private static void SetEnumProperty(Type ownerType, object target, string propertyName, string valueName)
		{
			var property = ownerType.GetProperty(propertyName);
			if (property == null || !property.PropertyType.IsEnum) {
				return;
			}

			try {
				property.SetValue(target, Enum.Parse(property.PropertyType, valueName));
			} catch (ArgumentException) {
				// HDRP enum values changed; fallback to the default behavior.
			}
		}

		private static Component GetHdAdditionalCameraData(GameObject gameObject)
		{
			if (!gameObject) {
				return null;
			}

			var hdCameraType = Type.GetType("UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData, Unity.RenderPipelines.HighDefinition.Runtime");
			return hdCameraType == null ? null : gameObject.GetComponent(hdCameraType);
		}

		private static void ApplyTransparentBackground(Texture2D beautyTexture, Texture2D alphaTexture, Color backgroundKeyColor)
		{
			if (!beautyTexture || !alphaTexture) {
				return;
			}

			var width = beautyTexture.width;
			var height = beautyTexture.height;
			if (width <= 0 || height <= 0 || alphaTexture.width != width || alphaTexture.height != height) {
				return;
			}

			var pixels = beautyTexture.GetPixels32();
			var alphaPixels = alphaTexture.GetPixels32();
			var backgroundColor = (Color32)backgroundKeyColor;
			var backgroundMask = FloodFillBackground(alphaPixels, width, height, backgroundColor);
			var foregroundMask = KeepLargestForegroundIsland(backgroundMask, width, height);

			for (var i = 0; i < pixels.Length; i++) {
				if (backgroundMask[i] || !foregroundMask[i]) {
					pixels[i] = new Color32(0, 0, 0, 0);
				} else {
					pixels[i].a = byte.MaxValue;
				}
			}

			beautyTexture.SetPixels32(pixels);
			beautyTexture.Apply(false, false);
		}

		private static bool[] FloodFillBackground(IReadOnlyList<Color32> pixels, int width, int height, Color32 backgroundColor)
		{
			var visited = new bool[pixels.Count];
			var queue = new Queue<int>();

			void EnqueueIfBackground(int index)
			{
				if (visited[index] || !IsBackgroundPixel(pixels[index], backgroundColor)) {
					return;
				}

				visited[index] = true;
				queue.Enqueue(index);
			}

			for (var x = 0; x < width; x++) {
				EnqueueIfBackground(x);
				EnqueueIfBackground(((height - 1) * width) + x);
			}

			for (var y = 1; y < height - 1; y++) {
				EnqueueIfBackground(y * width);
				EnqueueIfBackground((y * width) + (width - 1));
			}

			while (queue.Count > 0) {
				var index = queue.Dequeue();
				var x = index % width;
				var y = index / width;

				if (x > 0) {
					EnqueueIfBackground(index - 1);
				}
				if (x < width - 1) {
					EnqueueIfBackground(index + 1);
				}
				if (y > 0) {
					EnqueueIfBackground(index - width);
				}
				if (y < height - 1) {
					EnqueueIfBackground(index + width);
				}
			}

			return visited;
		}

		private static bool[] KeepLargestForegroundIsland(IReadOnlyList<bool> backgroundMask, int width, int height)
		{
			var visited = new bool[backgroundMask.Count];
			var keepMask = new bool[backgroundMask.Count];
			var bestRegion = Array.Empty<int>();

			for (var start = 0; start < backgroundMask.Count; start++) {
				if (backgroundMask[start] || visited[start]) {
					continue;
				}

				var region = new List<int>();
				var queue = new Queue<int>();
				queue.Enqueue(start);
				visited[start] = true;

				while (queue.Count > 0) {
					var index = queue.Dequeue();
					region.Add(index);
					var x = index % width;
					var y = index / width;

					void TryEnqueue(int nextIndex)
					{
						if (backgroundMask[nextIndex] || visited[nextIndex]) {
							return;
						}

						visited[nextIndex] = true;
						queue.Enqueue(nextIndex);
					}

					if (x > 0) {
						TryEnqueue(index - 1);
					}
					if (x < width - 1) {
						TryEnqueue(index + 1);
					}
					if (y > 0) {
						TryEnqueue(index - width);
					}
					if (y < height - 1) {
						TryEnqueue(index + width);
					}
				}

				if (region.Count > bestRegion.Length) {
					bestRegion = region.ToArray();
				}
			}

			foreach (var index in bestRegion) {
				keepMask[index] = true;
			}

			return keepMask;
		}

		private static bool IsBackgroundPixel(Color32 pixel, Color32 backgroundColor)
		{
			return Mathf.Abs(pixel.r - backgroundColor.r) <= BackgroundTolerance &&
				   Mathf.Abs(pixel.g - backgroundColor.g) <= BackgroundTolerance &&
				   Mathf.Abs(pixel.b - backgroundColor.b) <= BackgroundTolerance &&
				   Mathf.Abs(pixel.a - backgroundColor.a) <= BackgroundTolerance;
		}
	}

	internal struct PackageScreenshotResult
	{
		public readonly string AssetPath;
		public readonly string AbsolutePath;
		public readonly Vector3 CameraPosition;
		public readonly float CameraDistance;
		public readonly Bounds PlayfieldBounds;
		public readonly Vector3 ReferenceCameraPosition;
		public readonly float ReferenceCameraDistance;

		public float PositionDelta => Vector3.Distance(CameraPosition, ReferenceCameraPosition);
		public float DistanceDelta => Mathf.Abs(CameraDistance - ReferenceCameraDistance);

		public PackageScreenshotResult(string assetPath, string absolutePath, Vector3 cameraPosition, float cameraDistance, Bounds playfieldBounds, Vector3 referenceCameraPosition, float referenceCameraDistance)
		{
			AssetPath = assetPath;
			AbsolutePath = absolutePath;
			CameraPosition = cameraPosition;
			CameraDistance = cameraDistance;
			PlayfieldBounds = playfieldBounds;
			ReferenceCameraPosition = referenceCameraPosition;
			ReferenceCameraDistance = referenceCameraDistance;
		}
	}
}
