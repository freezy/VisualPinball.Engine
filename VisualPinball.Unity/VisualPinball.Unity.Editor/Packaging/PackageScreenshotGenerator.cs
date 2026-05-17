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
using Unity.Mathematics;
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
		internal const TextureFormat ScreenshotTextureFormat = TextureFormat.RGBA32;
		internal const float FieldOfView = 3f;
		internal const string DefaultHdriAssetPath =      "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/Assets/studio_small_08_1k.exr";
		private const string CameraPrefabPath =           "Packages/org.visualpinball.engine.unity.hdrp/Assets/EditorResources/Prefabs/Screenshot/Camera.prefab";

		private static readonly Quaternion TopDownRotation = Quaternion.Euler(90f, 0f, 0f);

		private const float MarginFactor = 1.05f;
		private const float MinNearPadding = 0.25f;
		private const float MinFarPadding = 0.5f;
		private const byte BackgroundTolerance = 4;

		private const string FilenameLightOn = "table-on.png";
		private const string FilenameLightOff = "table-off.png";

		internal static PackageScreenshotResult Generate(TableComponent tableComponent, string outputFolderPath, Cubemap hdriCubemap, float hdriExposure)
		{
			if (!tableComponent) {
				throw new ArgumentNullException(nameof(tableComponent));
			}

			if (string.IsNullOrWhiteSpace(outputFolderPath)) {
				throw new ArgumentException("A valid output path is required.", nameof(outputFolderPath));
			}

			var playfield = tableComponent.GetComponentInChildren<PlayfieldComponent>(true);
			if (!playfield) {
				throw new InvalidOperationException("No PlayfieldComponent was found under the selected table.");
			}

			if (!hdriCubemap) {
				throw new ArgumentNullException(nameof(hdriCubemap), "A HDRI cubemap is required.");
			}

			var playfieldBounds = GetPlayfieldBounds(playfield);
			var tableBounds = tableComponent.GetTableBounds();
			var absolutePath = GetAbsolutePath(outputFolderPath);
			var camera = InstantiateCamera();

			Directory.CreateDirectory(absolutePath);

			RenderTexture renderTextureLightsOn = null;
			RenderTexture renderTextureLightsOff = null;
			Texture2D lightsOnTexture = null;
			Texture2D lightsOffTexture = null;
			var previousActive = RenderTexture.active;

			try {
				var playfieldCenter = playfieldBounds.center;
				var playfieldWidth = playfieldBounds.size.x;
				var playfieldHeight = playfieldBounds.size.z;
				// var referenceDistance = math.abs(referenceCamera.transform.position.y - playfieldCenter.y);
				renderTextureLightsOn = CreateRenderTexture("Package Screenshot Lights On");
				renderTextureLightsOff = CreateRenderTexture("Package Screenshot Lights Off");
				lightsOnTexture = new Texture2D(PortraitWidth, PortraitHeight, ScreenshotTextureFormat, false);
				lightsOffTexture = new Texture2D(PortraitWidth, PortraitHeight, ScreenshotTextureFormat, false);

				var cameraDistance = CalculateTopDownDistance(playfieldWidth, playfieldHeight, PortraitWidth / (float)PortraitHeight, camera);
				var cameraPosition = new Vector3(playfieldCenter.x, playfieldCenter.y + cameraDistance, playfieldCenter.z);
				camera.transform.SetPositionAndRotation(cameraPosition, TopDownRotation);
				var clipPlanes = CalculateClipPlanes(tableBounds, camera.transform);
				camera.nearClipPlane = clipPlanes.x;
				camera.farClipPlane = clipPlanes.y;

				using var environmentScope = PackageScreenshotEnvironmentProvider.CreateScope(tableComponent.transform, hdriCubemap, hdriExposure);
				// HDRP does not correctly drive exposure/tonemapping for the legacy
				// immediate Camera.Render() into an offscreen target (the image blows
				// out). Use the SRP-supported render request path instead.
				var renderRequest = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = renderTextureLightsOn };
				camera.SubmitRenderRequest(renderRequest);
				ReadRenderTexture(renderTextureLightsOn, lightsOnTexture);

				var lightsOnFilePath = Path.Combine(absolutePath, FilenameLightOn);
				var lightsOnAssetPath = $"{outputFolderPath}/{FilenameLightOn}";
				File.WriteAllBytes(lightsOnFilePath, lightsOnTexture.EncodeToPNG());
				AssetDatabase.ImportAsset(lightsOnAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

				return new PackageScreenshotResult(
					outputFolderPath,
					lightsOnFilePath,
					cameraPosition,
					cameraDistance,
					playfieldBounds
				);

			} finally {
				RenderTexture.active = previousActive;
				if (renderTextureLightsOn) {
					renderTextureLightsOn.Release();
				}
				if (renderTextureLightsOff) {
					renderTextureLightsOff.Release();
				}
				if (lightsOnTexture) {
					UnityEngine.Object.DestroyImmediate(lightsOnTexture);
				}
				if (lightsOffTexture) {
					UnityEngine.Object.DestroyImmediate(lightsOffTexture);
				}
				if (renderTextureLightsOn) {
					UnityEngine.Object.DestroyImmediate(renderTextureLightsOn);
				}
				if (renderTextureLightsOff) {
					UnityEngine.Object.DestroyImmediate(renderTextureLightsOff);
				}

				// if (camera) {
				// 	UnityEngine.Object.DestroyImmediate(camera.gameObject);
				// }
			}
		}

		private static T InstantiatePrefab<T>(string path, string name) where T : Behaviour
		{
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

			if (prefab == null) {
				Debug.LogError($"Could not find prefab at path: {CameraPrefabPath}");
				return null;
			}

			var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

			instance.name = name;
			return instance.GetComponent<T>();
		}
		private static Camera InstantiateCamera() => InstantiatePrefab<Camera>(CameraPrefabPath, "Screenshot Camera");
		
		private static RenderTexture CreateRenderTexture(string name)
		{
			var renderTexture = new RenderTexture(PortraitWidth, PortraitHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) {
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
			var width = math.abs(Physics.ScaleToWorld(playfield.Right - playfield.Left));
			var height = math.abs(Physics.ScaleToWorld(playfield.Bottom - playfield.Top));
			var centerLocal = new Vector3(
				Physics.ScaleToWorld((playfield.Left + playfield.Right) * 0.5f),
				0f,
				-Physics.ScaleToWorld((playfield.Top + playfield.Bottom) * 0.5f)
			);
			var centerWorld = playfield.transform.TransformPoint(centerLocal);
			var size = Vector3.Scale(new Vector3(width, 0.01f, height), Abs(playfield.transform.lossyScale));
			return new Bounds(centerWorld, size);
		}

		private static float CalculateTopDownDistance(float playfieldWidth, float playfieldHeight, float targetAspect, Camera camera)
		{
			if (!camera || targetAspect <= 0f) {
				throw new InvalidOperationException("Unable to calculate the screenshot camera distance from the configured FOV.");
			}

			var verticalFieldOfView = GetEffectiveVerticalFieldOfView(camera);
			var verticalHalfAngleTangent = Mathf.Tan(verticalFieldOfView * 0.5f * Mathf.Deg2Rad);
			var horizontalFieldOfView = Camera.VerticalToHorizontalFieldOfView(verticalFieldOfView, targetAspect);
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

		private static Vector3 Abs(Vector3 vector) => new(math.abs(vector.x), math.abs(vector.y), math.abs(vector.z));
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