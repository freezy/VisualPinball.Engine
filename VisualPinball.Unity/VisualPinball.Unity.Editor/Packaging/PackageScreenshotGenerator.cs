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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

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
		internal const string FilenameLightsOn = "table-lights-on.png";
		// Shot 2: env (directional) + HDRI, all playfield lamps off (incl. faux bulb).
		internal const string FilenameLightsOff = "table-lights-off.png";
		// Shot 3: HDRI only, no directional, playfield lamps on (flashers off).
		internal const string FilenameHdriOnly = "table-hdri.png";

		// The generated screenshot files the packager pulls into the .vpe (in shot order).
		internal static readonly string[] ScreenshotFileNames = { FilenameLightsOn, FilenameLightsOff, FilenameHdriOnly };

		// Sidecar describing the table's pixel rect within the screenshots (shared by all shots),
		// so the player can crop them to the table and drop the surrounding background.
		internal const string FilenameBounds = "table-bounds.json";

		// Warm-up render frames so HDRP temporal screen-space effects (SSGI/SSR/
		// SSAO) converge before the screenshot is read. Overridable via EditorPrefs.
		private const string WarmupFramesPrefKey = "VisualPinball.Unity.Editor.PackageScreenshotGenerator.WarmupFrames";
		private const int DefaultWarmupFrames = 16;

		// Capture spans several editor frames on purpose: each shot toggles lamp/bulb
		// state, but HDRP only reconciles culling and GPU-resident-drawer instance data on
		// a PlayerLoop tick, not within a synchronous SubmitRenderRequest loop. Rendering a
		// shot in the same frame its state was applied captures the previously settled
		// state (e.g. bulbs that were on before the run leak into the "off" shots). So we
		// apply a shot's state, let a few frames elapse, then render it - which makes
		// Generate asynchronous: the result arrives via onComplete.
		private const int SettleFrames = 3;

		internal static void Generate(TableComponent tableComponent, string outputFolderPath, Cubemap hdriCubemap, float hdriExposure,
			Action<PackageScreenshotResult> onComplete, Action<Exception> onError = null)
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

			var routine = GenerateRoutine(tableComponent, outputFolderPath, hdriCubemap, hdriExposure, playfield, onComplete);
			EditorApplication.CallbackFunction driver = null;
			driver = () => {
				bool moveNext;
				try {
					moveNext = routine.MoveNext();
				} catch (Exception ex) {
					EditorApplication.update -= driver;
					if (onError != null) {
						onError(ex);
					} else {
						Debug.LogException(ex);
					}
					return;
				}
				if (!moveNext) {
					EditorApplication.update -= driver;
				}
			};
			EditorApplication.update += driver;
		}

		private static IEnumerator GenerateRoutine(TableComponent tableComponent, string outputFolderPath, Cubemap hdriCubemap, float hdriExposure,
			PlayfieldComponent playfield, Action<PackageScreenshotResult> onComplete)
		{
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
			var hiddenObjects = new HiddenObjectSnapshot(tableComponent);

			PackageScreenshotResult result = default;
			var succeeded = false;
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
				string boundsJson = null;

				// Hide the cabinet/backbox (marker components) for every shot. The per-shot
				// settle frames below let HDRP reconcile the deactivation before the render.
				hiddenObjects.Hide();

				for (var shotIndex = 0; shotIndex < shots.Length; shotIndex++) {
					var shot = shots[shotIndex];
					playfieldLights.Apply(shot.Lights);

					// Let HDRP reconcile culling / GPU-resident-drawer instance data for the
					// just-applied state before rendering; otherwise the render captures the
					// previously settled state and bulbs leak between shots.
					for (var f = 0; f < SettleFrames; f++) {
						yield return null;
					}

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
							boundsJson = BuildBoundsJson(camera, playfieldWidth, playfieldHeight, cameraDistance);
						}

						using (PackageScreenshotEnvironmentProvider.CreateScope(tableComponent.transform, hdriCubemap, hdriExposure, shot.IncludeDirectionalLight)) {
							// HDRP does not correctly drive exposure/tonemapping for the
							// legacy immediate Camera.Render() into an offscreen target
							// (the image blows out). Use the SRP render request path.
							var renderRequest = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = renderTexture };
							// HDRP SSGI/SSR/SSAO are temporally accumulated; a single
							// offscreen frame is unconverged (darker indirect/recesses)
							// vs the continuously-rendered game view. Submit several
							// warm-up frames so temporal accumulation converges, then
							// read the last one. Tunable via EditorPrefs without a recompile.
							var warmupFrames = Mathf.Max(1, EditorPrefs.GetInt(WarmupFramesPrefKey, DefaultWarmupFrames));
							for (var w = 0; w < warmupFrames; w++) {
								camera.SubmitRenderRequest(renderRequest);
							}
							ReadRenderTexture(renderTexture, readbackTexture);
						}
					} finally {
						Object.DestroyImmediate(camera.gameObject);
					}

					var filePath = Path.Combine(absolutePath, shot.FileName);
					var assetPath = $"{outputFolderPath}/{shot.FileName}";
					WriteScreenshotFile(assetPath, filePath, readbackTexture.EncodeToPNG());
					AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
					primaryFilePath ??= filePath;
				}

				// Write the table crop-bounds sidecar for the player to crop the screenshots.
				if (boundsJson != null) {
					File.WriteAllText(Path.Combine(absolutePath, FilenameBounds), boundsJson);
					AssetDatabase.ImportAsset($"{outputFolderPath}/{FilenameBounds}", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
				}

				result = new PackageScreenshotResult(
					outputFolderPath,
					primaryFilePath,
					resultCameraPosition,
					resultCameraDistance,
					playfieldBounds
				);
				succeeded = true;

			} finally {
				playfieldLights.Restore();
				hiddenObjects.Restore();
				RenderTexture.active = previousActive;
				if (readbackTexture) {
					Object.DestroyImmediate(readbackTexture);
				}
				if (renderTexture) {
					renderTexture.Release();
					Object.DestroyImmediate(renderTexture);
				}
			}

			if (succeeded) {
				onComplete?.Invoke(result);
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
		/// Captures/restores lamp state for the screenshots. Lamps are enumerated
		/// like the LampManager - via TableComponent.MappingConfig.Lamps, IsCoil =
		/// flasher, LightGroupComponent references resolved into member lamps, plus
		/// every remaining LightComponent for coverage.
		/// </summary>
		/// <remarks>
		/// Each lamp is toggled with the exact same mechanism as
		/// <c>LampManager.SetLampDeviceEnabled</c>: flip the Unity <c>Light.enabled</c>
		/// directly (edit-time runtime caches aren't initialized), then set
		/// <see cref="LightComponent.Enabled"/>. The latter hides/shows the lamp's
		/// emissive faux-bulb meshes (via SetActive in edit mode), which is what keeps
		/// the bulbs from showing in the "off" shots. It only toggles the emissive mesh
		/// children, never the Light "Source" GameObject, so
		/// <c>LightComponent.LightSources</c> stays intact and the manager keeps working.
		/// "On" enables every non-flasher lamp regardless of its edit-time default;
		/// flashers stay off. Group lamps (LightGroupComponent) whose sub-lamps are raw
		/// Lights + faux bulbs without their own LightComponent (e.g. L51's skull eyes)
		/// are handled separately, since LightComponent.Enabled cannot reach them.
		/// </remarks>
		private sealed class PlayfieldLightSnapshot
		{
			private readonly struct Entry
			{
				public readonly LightComponent Lamp;
				public readonly bool IsFlasher;
				public readonly Light[] Lights;
				public readonly bool[] LightEnabled;
				// Emissive meshes that LightComponent.Enabled drives (the faux bulbs),
				// captured so their visibility/emissive can be restored afterwards.
				public readonly Renderer[] EmissiveRenderers;
				public readonly bool[] RendererActive;

				public Entry(LightComponent lamp, bool isFlasher, Light[] lights, bool[] lightEnabled, Renderer[] emissiveRenderers, bool[] rendererActive)
				{
					Lamp = lamp;
					IsFlasher = isFlasher;
					Lights = lights;
					LightEnabled = lightEnabled;
					EmissiveRenderers = emissiveRenderers;
					RendererActive = rendererActive;
				}
			}

			private readonly List<Entry> _entries = new();

			public PlayfieldLightSnapshot(TableComponent tableComponent)
			{
				// Flashers are identified via the lamp mapping (IsCoil), resolving
				// LightGroupComponent references into member lamps. But the mapping
				// does NOT cover every LightComponent (groups, null-device coil
				// mappings, unmapped lamps - here 82 mappings vs 110 lamps), so we
				// must enumerate ALL LightComponents for coverage; otherwise the
				// uncovered lamps keep their bulb lit in the off shots ("bulbs back",
				// non-deterministic since the uncovered set shifts with table state).
				var flashers = new HashSet<LightComponent>();
				var flasherDevices = new HashSet<Component>();
				var lamps = tableComponent.MappingConfig?.Lamps;
				if (lamps != null) {
					var resolved = new List<LightComponent>();
					foreach (var lampMapping in lamps) {
						if (lampMapping == null || !lampMapping.IsCoil || lampMapping.Device == null) {
							continue;
						}
						if (lampMapping.Device is Component devComp && devComp) {
							flasherDevices.Add(devComp);
						}
						resolved.Clear();
						ResolveLightComponents(lampMapping.Device, resolved);
						foreach (var lc in resolved) {
							if (lc) {
								flashers.Add(lc);
							}
						}
					}
				}

				foreach (var lamp in tableComponent.GetComponentsInChildren<LightComponent>(true)) {
					if (!lamp) {
						continue;
					}
					// Mapping-resolved flasher, or the VPX "F_" naming fallback for
					// coil mappings that have no wired device.
					var isFlasher = flashers.Contains(lamp) || lamp.name.StartsWith("F_", StringComparison.Ordinal);
					AddEntry(lamp, isFlasher);
				}

				// Group lamps (LightGroupComponent) can drive raw Lights + faux-bulb meshes
				// that are NOT wrapped in their own LightComponent (e.g. L51's two skull-eye
				// sub-lamps). The LightComponent pass above misses those, leaving their bulbs
				// lit in the off shots, so collect each group's orphan lights/bulbs here.
				foreach (var group in tableComponent.GetComponentsInChildren<LightGroupComponent>(true)) {
					if (!group) {
						continue;
					}
					var isFlasher = flasherDevices.Contains(group) || group.name.StartsWith("F_", StringComparison.Ordinal);
					AddOrphanGroupEntry(group, isFlasher);
				}
			}

			private void AddEntry(LightComponent lamp, bool isFlasher)
			{
				var lights = lamp.GetComponentsInChildren<Light>(true);
				var lightEnabled = new bool[lights.Length];
				for (var i = 0; i < lights.Length; i++) {
					lightEnabled[i] = lights[i].enabled;
				}

				// Capture the same emissive meshes LightComponent.Enabled drives in edit
				// mode (emissive MeshRenderers under the lamp - the faux bulbs), so we can
				// restore their visibility and clear the emissive block it pushes.
				var converter = RenderPipeline.Current?.MaterialConverter;
				var rendererList = new List<Renderer>();
				if (converter != null) {
					foreach (var mr in lamp.GetComponentsInChildren<MeshRenderer>(true)) {
						if (mr && converter.GetEmissiveIntensity(mr.sharedMaterial) > 0f) {
							rendererList.Add(mr);
						}
					}
				}
				var emissiveRenderers = rendererList.ToArray();
				var rendererActive = new bool[emissiveRenderers.Length];
				for (var i = 0; i < emissiveRenderers.Length; i++) {
					rendererActive[i] = emissiveRenderers[i].gameObject.activeSelf;
				}

				_entries.Add(new Entry(lamp, isFlasher, lights, lightEnabled, emissiveRenderers, rendererActive));
			}

			// A LightGroupComponent whose member sub-lamps have no LightComponent of their
			// own (raw Light + faux bulb, e.g. L51's skull eyes) - capture those orphan
			// lights and emissive faux-bulb meshes so the off shots turn them off / hide
			// them too. Entries created here have a null Lamp (no LightComponent to drive).
			private void AddOrphanGroupEntry(LightGroupComponent group, bool isFlasher)
			{
				var converter = RenderPipeline.Current?.MaterialConverter;

				var lightList = new List<Light>();
				foreach (var l in group.GetComponentsInChildren<Light>(true)) {
					if (l && l.GetComponentInParent<LightComponent>(true) == null) {
						lightList.Add(l);
					}
				}

				var rendererList = new List<Renderer>();
				if (converter != null) {
					foreach (var mr in group.GetComponentsInChildren<MeshRenderer>(true)) {
						if (mr && converter.GetEmissiveIntensity(mr.sharedMaterial) > 0f && mr.GetComponentInParent<LightComponent>(true) == null) {
							rendererList.Add(mr);
						}
					}
				}

				if (lightList.Count == 0 && rendererList.Count == 0) {
					return;
				}

				var lights = lightList.ToArray();
				var lightEnabled = new bool[lights.Length];
				for (var i = 0; i < lights.Length; i++) {
					lightEnabled[i] = lights[i].enabled;
				}
				var emissiveRenderers = rendererList.ToArray();
				var rendererActive = new bool[emissiveRenderers.Length];
				for (var i = 0; i < emissiveRenderers.Length; i++) {
					rendererActive[i] = emissiveRenderers[i].gameObject.activeSelf;
				}

				_entries.Add(new Entry(null, isFlasher, lights, lightEnabled, emissiveRenderers, rendererActive));
			}

			// Resolve a mapped lamp device to the concrete LightComponents it drives,
			// expanding LightGroupComponent references recursively.
			private static void ResolveLightComponents(ILampDeviceComponent device, List<LightComponent> result)
			{
				switch (device) {
					case LightComponent lightComponent:
						result.Add(lightComponent);
						break;
					case LightGroupComponent group:
						foreach (var child in group.Lights) {
							if (child != null) {
								ResolveLightComponents(child, result);
							}
						}
						break;
				}
			}

			public void Apply(PlayfieldLightMode mode)
			{
				foreach (var entry in _entries) {
					// Flashers are always off; "On" lights every other lamp (inserts
					// AND GI) regardless of its edit-time default.
					var on = mode == PlayfieldLightMode.On && !entry.IsFlasher;

					// Same mechanism as LampManager.SetLampDeviceEnabled: flip the Unity
					// lights directly (edit-time runtime caches aren't initialized), then
					// drive LightComponent.Enabled, which hides the faux-bulb meshes
					// (SetActive in edit mode) - so the off shots no longer leave bulbs
					// visible. The Light lives on the "Source" GameObject, which this
					// never deactivates, so LightComponent.LightSources stays intact.
					foreach (var light in entry.Lights) {
						if (light) {
							light.enabled = on;
						}
					}
					if (entry.Lamp) {
						entry.Lamp.Enabled = on;
					} else {
						// Orphan group-lamp meshes have no LightComponent to drive them; hide/
						// show them directly - the same SetActive mechanism Enabled uses in edit mode.
						foreach (var r in entry.EmissiveRenderers) {
							if (r) {
								r.gameObject.SetActive(on);
							}
						}
					}

					// For "on", clear any emissive property block so the bulb shows its
					// (warmer) baked emissive, matching the un-toggled look. For "off" the
					// mesh is deactivated, so the block is moot.
					if (on) {
						foreach (var r in entry.EmissiveRenderers) {
							if (r) {
								r.SetPropertyBlock(null);
							}
						}
					}
				}
			}

			public void Restore()
			{
				foreach (var entry in _entries) {
					for (var i = 0; i < entry.Lights.Length; i++) {
						if (entry.Lights[i]) {
							entry.Lights[i].enabled = entry.LightEnabled[i];
						}
					}
					// Drop the emissive override LightComponent.Enabled pushed (so the
					// baked value shows again) and restore each mesh's original visibility.
					for (var i = 0; i < entry.EmissiveRenderers.Length; i++) {
						var r = entry.EmissiveRenderers[i];
						if (!r) {
							continue;
						}
						r.SetPropertyBlock(null);
						r.gameObject.SetActive(entry.RendererActive[i]);
					}
				}
			}

		}

		// Remembers and restores the active state of the cabinet/backbox marker objects, so
		// the screenshot generator can hide them for the capture and put them back after.
		private sealed class HiddenObjectSnapshot
		{
			private readonly GameObject[] _objects;
			private readonly bool[] _active;

			public HiddenObjectSnapshot(TableComponent tableComponent)
			{
				var objects = new List<GameObject>();
				foreach (var cabinet in tableComponent.GetComponentsInChildren<CabinetComponent>(true)) {
					if (cabinet && !objects.Contains(cabinet.gameObject)) {
						objects.Add(cabinet.gameObject);
					}
				}
				foreach (var backbox in tableComponent.GetComponentsInChildren<BackboxComponent>(true)) {
					if (backbox && !objects.Contains(backbox.gameObject)) {
						objects.Add(backbox.gameObject);
					}
				}
				_objects = objects.ToArray();
				_active = new bool[_objects.Length];
				for (var i = 0; i < _objects.Length; i++) {
					_active[i] = _objects[i].activeSelf;
				}
			}

			public void Hide()
			{
				foreach (var go in _objects) {
					if (go) {
						go.SetActive(false);
					}
				}
			}

			public void Restore()
			{
				for (var i = 0; i < _objects.Length; i++) {
					if (_objects[i]) {
						_objects[i].SetActive(_active[i]);
					}
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

		private static void WriteScreenshotFile(string assetPath, string filePath, byte[] data)
		{
			// Deleting the existing asset makes Unity drop every handle to it -
			// memory-map, Inspector preview, selection - which UnloadUnusedAssets
			// alone cannot do. This is what makes a re-generate reliable: without
			// it, a still-previewed previous screenshot keeps the file locked, the
			// write throws Win32 1224, and the run aborts leaving the other shots
			// stale (the recurring "bulbs are back" symptom).
			if (AssetDatabase.LoadAssetAtPath<Object>(assetPath)) {
				AssetDatabase.DeleteAsset(assetPath);
			}
			try {
				File.WriteAllBytes(filePath, data);
			} catch (IOException) {
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

		[Serializable]
		private struct ScreenshotBounds
		{
			public int imageWidth;
			public int imageHeight;
			public int cropX;
			public int cropY;
			public int cropWidth;
			public int cropHeight;
		}

		// The table fills a centered rect of the screenshot; derive it from the same framing
		// math the camera distance uses (CalculateTopDownDistance) so it exactly matches the
		// render. Origin is top-left, matching the encoded PNG/WebP. All shots share one camera
		// framing, so a single rect applies to every screenshot.
		private static string BuildBoundsJson(Camera camera, float playfieldWidth, float playfieldHeight, float cameraDistance)
		{
			var verticalFieldOfView = GetEffectiveVerticalFieldOfView(camera);
			var verticalHalfTan = Mathf.Tan(verticalFieldOfView * 0.5f * Mathf.Deg2Rad);
			var aspect = PortraitWidth / (float)PortraitHeight;
			var horizontalFieldOfView = Camera.VerticalToHorizontalFieldOfView(verticalFieldOfView, aspect);
			var horizontalHalfTan = Mathf.Tan(horizontalFieldOfView * 0.5f * Mathf.Deg2Rad);

			var frameHeight = 2f * cameraDistance * verticalHalfTan;
			var frameWidth = 2f * cameraDistance * horizontalHalfTan;
			var fractionVertical = frameHeight > 0f ? Mathf.Clamp01(playfieldHeight / frameHeight) : 1f;
			var fractionHorizontal = frameWidth > 0f ? Mathf.Clamp01(playfieldWidth / frameWidth) : 1f;

			var cropWidth = Mathf.RoundToInt(fractionHorizontal * PortraitWidth);
			var cropHeight = Mathf.RoundToInt(fractionVertical * PortraitHeight);
			var bounds = new ScreenshotBounds {
				imageWidth = PortraitWidth,
				imageHeight = PortraitHeight,
				cropX = Mathf.RoundToInt((PortraitWidth - cropWidth) * 0.5f),
				cropY = Mathf.RoundToInt((PortraitHeight - cropHeight) * 0.5f),
				cropWidth = cropWidth,
				cropHeight = cropHeight,
			};
			return JsonUtility.ToJson(bounds, true);
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