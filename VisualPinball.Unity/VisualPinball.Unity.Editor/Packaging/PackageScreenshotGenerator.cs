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
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public interface IPackageScreenshotEnvironmentProvider
	{
		IDisposable CreateEnvironmentScope(Transform tableRoot, Cubemap hdriCubemap);
	}

	public static class PackageScreenshotEnvironmentProvider
	{
		private static IPackageScreenshotEnvironmentProvider _active;

		public static void Register(IPackageScreenshotEnvironmentProvider provider)
		{
			_active = provider;
		}

		public static IDisposable CreateScope(Transform tableRoot, Cubemap hdriCubemap)
		{
			return _active?.CreateEnvironmentScope(tableRoot, hdriCubemap);
		}
	}

	internal static class PackageScreenshotGenerator
	{
		internal const int PortraitWidth = 2160;
		internal const int PortraitHeight = 3840;
		internal const float FieldOfView = 3f;
		internal const string DefaultHdriAssetPath = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/Assets/studio_small_08_1k.exr";

		private const byte BackgroundTolerance = 12;

		private static readonly Color Transparent = new(0f, 0f, 0f, 0f);

		internal static string GetSuggestedFileName(TableComponent tableComponent)
			=> $"{tableComponent.name}-package-preview";

		internal static PackageScreenshotResult Generate(TableComponent tableComponent, string assetPath, Cubemap hdriCubemap)
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
			var absolutePath = GetAbsolutePath(assetPath);
			var referenceCamera = FindReferenceCamera(tableComponent);

			Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

			GameObject cameraGo = null;
			RenderTexture renderTexture = null;
			Texture2D texture = null;
			var previousActive = RenderTexture.active;

			try {
				cameraGo = new GameObject("Package Screenshot Camera") {
					hideFlags = HideFlags.HideAndDontSave,
				};

				var camera = cameraGo.AddComponent<Camera>();
				CopyCameraSettings(referenceCamera, cameraGo, camera);
				camera.enabled = false;
				camera.transform.SetPositionAndRotation(referenceCamera.transform.position, referenceCamera.transform.rotation);
				camera.forceIntoRenderTexture = true;
				camera.clearFlags = CameraClearFlags.SolidColor;
				camera.backgroundColor = Transparent;
				TryConfigureHdrpCamera(cameraGo, forceTransparentBackground: true);
				using var environmentScope = PackageScreenshotEnvironmentProvider.CreateScope(tableComponent.transform, hdriCubemap);

				var cameraPosition = camera.transform.position;
				var cameraDistance = Vector3.Distance(cameraPosition, playfieldBounds.center);

				renderTexture = new RenderTexture(PortraitWidth, PortraitHeight, 24, RenderTextureFormat.ARGB32) {
					antiAliasing = 1,
					name = "Package Screenshot RenderTexture",
				};
				renderTexture.Create();

				texture = new Texture2D(PortraitWidth, PortraitHeight, TextureFormat.RGBA32, false);

				camera.targetTexture = renderTexture;
				camera.Render();

				RenderTexture.active = renderTexture;
				texture.ReadPixels(new Rect(0f, 0f, PortraitWidth, PortraitHeight), 0, 0, false);
				texture.Apply(false, false);
				ApplyTransparentBackground(texture);

				File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
				AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

				return new PackageScreenshotResult(
					assetPath,
					absolutePath,
					cameraPosition,
					cameraDistance,
					playfieldBounds
				);
			} finally {
				RenderTexture.active = previousActive;
				if (renderTexture) {
					renderTexture.Release();
				}
				if (texture) {
					UnityEngine.Object.DestroyImmediate(texture);
				}
				if (renderTexture) {
					UnityEngine.Object.DestroyImmediate(renderTexture);
				}
				if (cameraGo) {
					UnityEngine.Object.DestroyImmediate(cameraGo);
				}
			}
		}

		private static Bounds GetPlayfieldBounds(PlayfieldComponent playfield)
		{
			if (playfield.TryGetComponent<Renderer>(out var renderer)) {
				return renderer.bounds;
			}

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

		private static void TryConfigureHdrpCamera(GameObject cameraGo, bool forceTransparentBackground)
		{
			var hdCameraType = Type.GetType("UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData, Unity.RenderPipelines.HighDefinition.Runtime");
			if (hdCameraType == null) {
				return;
			}

			var hdCamera = cameraGo.GetComponent(hdCameraType) ?? cameraGo.AddComponent(hdCameraType);
			if (forceTransparentBackground) {
				SetEnumProperty(hdCameraType, hdCamera, "clearColorMode", "Color");
			}

			var backgroundProperty = hdCameraType.GetProperty("backgroundColorHDR");
			if (forceTransparentBackground) {
				backgroundProperty?.SetValue(hdCamera, Transparent);
			}
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

		private static void ApplyTransparentBackground(Texture2D texture)
		{
			if (!texture) {
				return;
			}

			var width = texture.width;
			var height = texture.height;
			if (width <= 0 || height <= 0) {
				return;
			}

			var pixels = texture.GetPixels32();
			var backgroundColor = SampleBackgroundColor(pixels, width, height);
			var backgroundMask = FloodFillBackground(pixels, width, height, backgroundColor);
			var foregroundMask = KeepLargestForegroundIsland(backgroundMask, width, height);

			for (var i = 0; i < pixels.Length; i++) {
				if (!backgroundMask[i] && foregroundMask[i]) {
					continue;
				}

				pixels[i] = new Color32(0, 0, 0, 0);
			}

			texture.SetPixels32(pixels);
			texture.Apply(false, false);
		}

		private static Color32 SampleBackgroundColor(IReadOnlyList<Color32> pixels, int width, int height)
		{
			var red = 0L;
			var green = 0L;
			var blue = 0L;
			var alpha = 0L;
			var samples = 0L;

			void Accumulate(int x, int y)
			{
				var pixel = pixels[(y * width) + x];
				red += pixel.r;
				green += pixel.g;
				blue += pixel.b;
				alpha += pixel.a;
				samples++;
			}

			for (var x = 0; x < width; x++) {
				Accumulate(x, 0);
				Accumulate(x, height - 1);
			}

			for (var y = 1; y < height - 1; y++) {
				Accumulate(0, y);
				Accumulate(width - 1, y);
			}

			if (samples == 0) {
				return new Color32(0, 0, 0, 0);
			}

			return new Color32(
				(byte)(red / samples),
				(byte)(green / samples),
				(byte)(blue / samples),
				(byte)(alpha / samples)
			);
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

		public PackageScreenshotResult(string assetPath, string absolutePath, Vector3 cameraPosition, float cameraDistance, Bounds playfieldBounds)
		{
			AssetPath = assetPath;
			AbsolutePath = absolutePath;
			CameraPosition = cameraPosition;
			CameraDistance = cameraDistance;
			PlayfieldBounds = playfieldBounds;
		}
	}
}
