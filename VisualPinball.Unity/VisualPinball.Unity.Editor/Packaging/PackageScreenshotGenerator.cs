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
		IDisposable CreateEnvironmentScope(Transform tableRoot, Cubemap hdriCubemap, float hdriExposure, bool includeDirectionalLight);
	}

	public static class PackageScreenshotEnvironmentProvider
	{
		private static IPackageScreenshotEnvironmentProvider _active;

		public static void Register(IPackageScreenshotEnvironmentProvider provider)
		{
			_active = provider;
		}

		public static IDisposable CreateScope(Transform tableRoot, Cubemap hdriCubemap, float hdriExposure, bool includeDirectionalLight)
		{
			return _active?.CreateEnvironmentScope(tableRoot, hdriCubemap, hdriExposure, includeDirectionalLight);
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

		// Shot 1: env (directional) + HDRI, playfield lamps on (flashers off).
		private const string FilenameLightsOn = "table-lights-on.png";
		// Shot 2: env (directional) + HDRI, all playfield lamps off (incl. faux bulb).
		private const string FilenameLightsOff = "table-lights-off.png";
		// Shot 3: HDRI only, no directional, playfield lamps on (flashers off).
		private const string FilenameHdriOnly = "table-hdri.png";

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

			Directory.CreateDirectory(absolutePath);

			// A previously generated screenshot that is still selected/pinged is
			// memory-mapped by Unity; overwriting it then throws Win32 1224 and
			// aborts the whole run (leaving the other shots stale). Release it.
			ReleaseOutputFileLocks(outputFolderPath);

			RenderTexture renderTexture = null;
			Texture2D readbackTexture = null;
			var previousActive = RenderTexture.active;
			var playfieldLights = new PlayfieldLightSnapshot(tableComponent);

			try {
				var playfieldCenter = playfieldBounds.center;
				var playfieldWidth = playfieldBounds.size.x;
				var playfieldHeight = playfieldBounds.size.z;
				renderTexture = CreateRenderTexture("Package Screenshot");
				readbackTexture = new Texture2D(PortraitWidth, PortraitHeight, ScreenshotTextureFormat, false);

				// HDRI is applied for all three shots; only the directional light is
				// toggled. Both "off" shots turn all lamps off and hide their bulbs;
				// they differ only in the directional light. Each shot uses a fresh
				// camera: HDRP caches culling/exposure per camera across synchronous
				// SubmitRenderRequest calls, so reusing one camera would render the
				// later shots with the first shot's lighting and geometry.
				var shots = new[] {
					new ShotConfig(FilenameLightsOn, true, PlayfieldLightMode.On),
					new ShotConfig(FilenameLightsOff, true, PlayfieldLightMode.Off),
					new ShotConfig(FilenameHdriOnly, false, PlayfieldLightMode.Off),
				};

				string primaryFilePath = null;
				var resultCameraPosition = Vector3.zero;
				var resultCameraDistance = 0f;

				for (var shotIndex = 0; shotIndex < shots.Length; shotIndex++) {
					var shot = shots[shotIndex];
					playfieldLights.Apply(shot.Lights);

					var camera = InstantiateCamera();
					try {
						var cameraDistance = CalculateTopDownDistance(playfieldWidth, playfieldHeight, PortraitWidth / (float)PortraitHeight, camera);
						var cameraPosition = new Vector3(playfieldCenter.x, playfieldCenter.y + cameraDistance, playfieldCenter.z);
						camera.transform.SetPositionAndRotation(cameraPosition, TopDownRotation);
						var clipPlanes = CalculateClipPlanes(tableBounds, camera.transform);
						camera.nearClipPlane = clipPlanes.x;
						camera.farClipPlane = clipPlanes.y;

						if (shotIndex == 0) {
							resultCameraPosition = cameraPosition;
							resultCameraDistance = cameraDistance;
						}

						using (PackageScreenshotEnvironmentProvider.CreateScope(tableComponent.transform, hdriCubemap, hdriExposure, shot.IncludeDirectionalLight)) {
							// HDRP does not correctly drive exposure/tonemapping for the
							// legacy immediate Camera.Render() into an offscreen target
							// (the image blows out). Use the SRP render request path.
							var renderRequest = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = renderTexture };
							camera.SubmitRenderRequest(renderRequest);
							ReadRenderTexture(renderTexture, readbackTexture);
						}
					} finally {
						UnityEngine.Object.DestroyImmediate(camera.gameObject);
					}

					var filePath = Path.Combine(absolutePath, shot.FileName);
					WriteScreenshotFile(filePath, readbackTexture.EncodeToPNG());
					AssetDatabase.ImportAsset($"{outputFolderPath}/{shot.FileName}", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
					primaryFilePath ??= filePath;
				}

				return new PackageScreenshotResult(
					outputFolderPath,
					primaryFilePath,
					resultCameraPosition,
					resultCameraDistance,
					playfieldBounds
				);

			} finally {
				playfieldLights.Restore();
				RenderTexture.active = previousActive;
				if (readbackTexture) {
					UnityEngine.Object.DestroyImmediate(readbackTexture);
				}
				if (renderTexture) {
					renderTexture.Release();
					UnityEngine.Object.DestroyImmediate(renderTexture);
				}
			}
		}

		private enum PlayfieldLightMode
		{
			// Non-flasher lamps (inserts + GI) on, flashers off.
			On,
			// All lamps off and their bulbs hidden.
			Off,
		}

		private readonly struct ShotConfig
		{
			public readonly string FileName;
			public readonly bool IncludeDirectionalLight;
			public readonly PlayfieldLightMode Lights;

			public ShotConfig(string fileName, bool includeDirectionalLight, PlayfieldLightMode lights)
			{
				FileName = fileName;
				IncludeDirectionalLight = includeDirectionalLight;
				Lights = lights;
			}
		}

		/// <summary>
		/// Captures the edit-time active state of every playfield lamp's light
		/// "Source" GameObject (the child that holds the Unity Light and, as its
		/// own child, the FauxBulb) so screenshots can be rendered with lamps on
		/// or fully off, then restored. Deactivating the Source removes the light
		/// AND the bulb in one go while leaving the insert artwork (its siblings)
		/// visible. Flashers are always off. "On" force-activates every non-flasher
		/// lamp (inserts and GI) regardless of its edit-time default.
		/// </summary>
		private sealed class PlayfieldLightSnapshot
		{
			private readonly struct Entry
			{
				public readonly bool IsFlasher;
				public readonly GameObject[] Sources;
				public readonly bool[] SourceActive;

				public Entry(bool isFlasher, GameObject[] sources, bool[] sourceActive)
				{
					IsFlasher = isFlasher;
					Sources = sources;
					SourceActive = sourceActive;
				}
			}

			private readonly List<Entry> _entries = new();

			public PlayfieldLightSnapshot(TableComponent tableComponent)
			{
				var flashers = CollectFlashers(tableComponent);
				foreach (var lamp in tableComponent.GetComponentsInChildren<LightComponent>(true)) {
					// The light "Source" GameObject holds the Unity Light and usually
					// the FauxBulb as its child, so toggling it covers both. A few
					// lamps parent the FauxBulb elsewhere ("Lamp"), so also collect
					// every FauxBulb GameObject directly as a belt-and-suspenders.
					var sourceList = new List<GameObject>();
					foreach (var light in lamp.GetComponentsInChildren<Light>(true)) {
						if (light && !sourceList.Contains(light.gameObject)) {
							sourceList.Add(light.gameObject);
						}
					}
					foreach (var mr in lamp.GetComponentsInChildren<MeshRenderer>(true)) {
						if (mr && mr.name.IndexOf("FauxBulb", StringComparison.OrdinalIgnoreCase) >= 0
							&& !sourceList.Contains(mr.gameObject)) {
							sourceList.Add(mr.gameObject);
						}
					}

					var sources = sourceList.ToArray();
					var sourceActive = new bool[sources.Length];
					for (var i = 0; i < sources.Length; i++) {
						sourceActive[i] = sources[i].activeSelf;
					}

					// MappingConfig resolves most flashers, but some coil mappings have
					// no wired device. VPX-imported flashers follow the "F_" naming
					// convention (regular lamps are "L*"), so use that as a fallback.
					var isFlasher = flashers.Contains(lamp) || lamp.name.StartsWith("F_", StringComparison.Ordinal);
					_entries.Add(new Entry(isFlasher, sources, sourceActive));
				}
			}

			public void Apply(PlayfieldLightMode mode)
			{
				foreach (var entry in _entries) {
					// Flashers are always off; "On" activates every other lamp's
					// Source (light + bulb), "Off" deactivates them all.
					var active = mode == PlayfieldLightMode.On && !entry.IsFlasher;
					foreach (var source in entry.Sources) {
						if (source) {
							source.SetActive(active);
						}
					}
				}
			}

			public void Restore()
			{
				foreach (var entry in _entries) {
					for (var i = 0; i < entry.Sources.Length; i++) {
						if (entry.Sources[i]) {
							entry.Sources[i].SetActive(entry.SourceActive[i]);
						}
					}
				}
			}

			private static HashSet<LightComponent> CollectFlashers(TableComponent tableComponent)
			{
				var flashers = new HashSet<LightComponent>();
				var lamps = tableComponent.MappingConfig?.Lamps;
				if (lamps == null) {
					return flashers;
				}
				foreach (var lamp in lamps) {
					if (lamp != null && lamp.IsCoil) {
						AddLampDevice(lamp.Device, flashers);
					}
				}
				return flashers;
			}

			private static void AddLampDevice(ILampDeviceComponent device, HashSet<LightComponent> flashers)
			{
				switch (device) {
					case LightComponent lightComponent:
						flashers.Add(lightComponent);
						break;
					case LightGroupComponent group:
						foreach (var child in group.Lights) {
							if (child != null) {
								AddLampDevice(child, flashers);
							}
						}
						break;
				}
			}
		}

		// Unity memory-maps a texture asset while it is selected/previewed, which
		// makes File.WriteAllBytes fail with Win32 1224. Clearing a selection that
		// points into the output folder and unloading unused assets releases it.
		private static void ReleaseOutputFileLocks(string outputFolderPath)
		{
			var selected = Selection.activeObject;
			if (selected) {
				var selectedPath = AssetDatabase.GetAssetPath(selected);
				if (!string.IsNullOrEmpty(selectedPath) &&
					selectedPath.Replace('\\', '/').StartsWith(outputFolderPath.Replace('\\', '/') + "/", StringComparison.OrdinalIgnoreCase)) {
					Selection.activeObject = null;
				}
			}
			EditorUtility.UnloadUnusedAssetsImmediate();
		}

		private static void WriteScreenshotFile(string filePath, byte[] data)
		{
			try {
				File.WriteAllBytes(filePath, data);
			} catch (IOException) {
				// The destination may still be memory-mapped by Unity; free it and
				// retry once before giving up.
				EditorUtility.UnloadUnusedAssetsImmediate();
				GC.Collect();
				GC.WaitForPendingFinalizers();
				File.WriteAllBytes(filePath, data);
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