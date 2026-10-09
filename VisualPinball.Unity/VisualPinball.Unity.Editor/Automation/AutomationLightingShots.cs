// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Stills of one insert with its Insert Light and materials set to a few
	/// values each, rendered through the Scene view, and the inspectors of its
	/// Insert Light, body material and cover by section, for the Lighting,
	/// Body Material and Cover pages of the documentation. What to shoot is
	/// described in "shots.json": the insert, an unlit neighbour sharing its
	/// material, and one entry per still. Everything a still changes is put
	/// back after the last one.
	/// </summary>
	public static class AutomationLightingShots
	{
		private const string BallPrefab = "Packages/org.visualpinball.unity.assets/Assets/Library/Balls/Metal - Steel Scratched.prefab";
		private static readonly string[] LampVectors = { "_OpticalLamp", "_OpticalLampB", "_OpticalLampC", "_OpticalLampD" };
		private static readonly string[] InsertSections = { "Geometry", "Bake", "Cover", "Preset" };
		private static readonly string[] LightSections = { "Lamp", "State", "Spill" };

		private static string Folder => Automation.Folder("lighting");

		[Serializable]
		private class Shot
		{
			public string name;
			public float intensity = 1f;
			public float output = 1f;
			public float[] color = { 1f, 0.64f, 0.35f };
			public bool tintWithLamp;
			public float tint = 1f;
			public float lumens = 1.5f;
			public float range = 0.1f;
			/// <summary>"x,y,z,w" in meters for each of the four bulbs, as the material stores them.</summary>
			public string[] lamps;
			/// <summary>The intensities of bulbs two to four.</summary>
			public float[] extra = { 1f, 1f, 1f };
			public bool ball;
			/// <summary>"_Property=value" on the body material.</summary>
			public string[] floats;
			/// <summary>"_Property=r,g,b" on the body material.</summary>
			public string[] colors;
			public string[] coverFloats;
			public string[] coverColors;
			/// <summary>Both inserts without their cover.</summary>
			public bool noCover;
			/// <summary>-1 leaves the playfield normals as they are, 0 turns them off, 1 on, on both inserts.</summary>
			public int playfieldNormals = -1;
			/// <summary>Whether the camera aims between the lit insert and the unlit one, rather than at the lit one.</summary>
			public bool frameBoth = true;
			public float[] camOffset = { 0f, 0.05f, -0.003f };
			public float[] lookOffset = { 0f, 0f, 0f };
			public int width = 900, height = 900;
		}

		[Serializable]
		private class Config
		{
			/// <summary>What the lit insert's name contains.</summary>
			public string insert;
			/// <summary>What the name of a neighbour sharing its material contains, kept off, so each still shows the material lit and unlit.</summary>
			public string unlit;
			public float[] ballOffset = { 0f, 0f, 0.02f };
			public Shot[] shots;
		}

		private struct MaterialValue
		{
			public bool IsColor;
			public float Value;
			public Color Color;
			public bool Overridden;
		}

		private struct LightValues
		{
			public float Intensity, Output, Tint, Lumens, Range;
			public Color Color;
			public bool TintWithLamp;
			public float[] ExtraIntensity;
			public Color[] ExtraColor;
		}

		private static Config _config;
		private static int _shot;
		private static int _passes;
		private static bool _running;
		private static InsertComponent _insert;
		private static InsertLightComponent _light;
		private static Material _body;
		private static Material _cover;
		private static InsertComponent _unlit;
		private static InsertLightComponent _unlitLight;
		private static float _unlitIntensity;
		private static LightValues _lightBackup;
		private static Vector4[] _lampBackup;
		// Which lamp vectors a material variant took from its parent.
		private static bool[] _lampInherited;
		private static GameObject _ball;
		private static Vector3 _pivot;
		private static Quaternion _rotation;
		private static float _size;

		// The materials' values as they were, and whether a variant had them overridden.
		private static readonly Dictionary<string, MaterialValue> BodyBackup = new();
		private static readonly Dictionary<string, MaterialValue> CoverBackup = new();
		// Each insert's cover and playfield normals as they were.
		private static readonly List<(InsertComponent insert, Material cover, bool normals)> ComponentBackup = new();

		private static float P(string s) => float.Parse(s, CultureInfo.InvariantCulture);

		private static Config LoadConfig() => JsonUtility.FromJson<Config>(File.ReadAllText(Path.Combine(Folder, "shots.json")));

		private static InsertComponent Find(string name) => Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(i => i.name.Contains(name));

		private static IEnumerable<InsertComponent> Both => new[] { _insert, _unlit }.Where(i => i != null);

		/// <summary>Renders every still of "shots.json" into a PNG named after it.</summary>
		[MenuItem(Automation.Menu + "Lighting/Capture")]
		public static void Capture()
		{
			// A second start would take its backup from the first one's stills.
			if (_running || AutomationShots.Refuse()) {
				return;
			}
			_config = LoadConfig();
			_insert = Find(_config.insert);
			_light = _insert.GetComponent<InsertLightComponent>();
			_body = _insert.GetComponent<MeshRenderer>().sharedMaterial;
			_unlit = string.IsNullOrEmpty(_config.unlit) ? null : Find(_config.unlit);
			_unlitLight = _unlit != null ? _unlit.GetComponent<InsertLightComponent>() : null;
			if (_unlit != null && _unlit.GetComponent<MeshRenderer>().sharedMaterial != _body) {
				Debug.LogError("Automation: the unlit insert wears another material than the lit one.");
				return;
			}
			if (_light.SpillLight == null) {
				Debug.LogError("Automation: the lit insert has no spill light.");
				return;
			}
			_running = true;
			AutomationShots.Acquire();
			_unlitIntensity = _unlitLight != null ? _unlitLight.Intensity : 0f;
			// Field by field: a JSON round trip would lose the spill light's scene reference.
			var bulbs = new SerializedObject(_light).FindProperty("ExtraBulbs");
			_lightBackup = new LightValues {
				Intensity = _light.Intensity, Output = _light.Output, Color = _light.Color, TintWithLamp = _light.TintWithLamp,
				Tint = _light.LampTint, Lumens = _light.SpillLumens, Range = _light.SpillRange,
				ExtraIntensity = Enumerable.Range(0, bulbs.arraySize).Select(i => bulbs.GetArrayElementAtIndex(i).FindPropertyRelative("Intensity").floatValue).ToArray(),
				ExtraColor = Enumerable.Range(0, bulbs.arraySize).Select(i => bulbs.GetArrayElementAtIndex(i).FindPropertyRelative("Color").colorValue).ToArray(),
			};
			_lampBackup = LampVectors.Select(v => _body.GetVector(v)).ToArray();
			_lampInherited = LampVectors.Select(v => _body.parent != null && !_body.IsPropertyOverriden(v)).ToArray();
			_cover = _insert.CoverMaterial;
			ComponentBackup.Clear();
			foreach (var insert in Both) {
				ComponentBackup.Add((insert, insert.CoverMaterial, insert.UsePlayfieldNormals));
			}
			var sceneView = SceneView.lastActiveSceneView;
			_pivot = sceneView.pivot;
			_rotation = sceneView.rotation;
			_size = sceneView.size;
			File.WriteAllText(Path.Combine(Folder, "frames.txt"), "");
			_shot = 0;
			Begin();
		}

		private static void Begin()
		{
			if (_shot >= _config.shots.Length) {
				Finish();
				return;
			}
			var shot = _config.shots[_shot];
			for (var i = 0; i < LampVectors.Length; i++) {
				var value = shot.lamps != null && i < shot.lamps.Length ? shot.lamps[i].Split(',').Select(P).ToArray() : null;
				_body.SetVector(LampVectors[i], value != null ? new Vector4(value[0], value[1], value[2], value[3]) : _lampBackup[i]);
			}
			SetMaterials(shot);
			SetComponents(shot);
			_light.Intensity = shot.intensity;
			_light.Output = shot.output;
			_light.Color = new Color(shot.color[0], shot.color[1], shot.color[2]);
			_light.TintWithLamp = shot.tintWithLamp;
			_light.LampTint = shot.tint;
			_light.SpillLumens = shot.lumens;
			_light.SpillRange = shot.range;
			if (shot.lamps != null) {
				var so = new SerializedObject(_light);
				var extra = so.FindProperty("ExtraBulbs");
				for (var i = 0; i < extra.arraySize && i < shot.extra.Length; i++) {
					extra.GetArrayElementAtIndex(i).FindPropertyRelative("Intensity").floatValue = shot.extra[i];
					extra.GetArrayElementAtIndex(i).FindPropertyRelative("Color").colorValue = _light.Color;
				}
				so.ApplyModifiedPropertiesWithoutUndo();
			}
			_light.Apply();
			if (_unlitLight != null) {
				_unlitLight.Intensity = 0f;
				_unlitLight.Apply();
			}
			if (shot.ball && _ball == null) {
				var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefab);
				if (prefab != null) {
					_ball = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
					_ball.name = "Automation Ball";
					var radius = _ball.GetComponentInChildren<Renderer>().bounds.extents.y;
					_ball.transform.position = _insert.transform.position + new Vector3(_config.ballOffset[0], _config.ballOffset[1] + radius, _config.ballOffset[2]);
				} else {
					Debug.LogWarning($"Automation: no ball at {BallPrefab}.");
				}
			}
			if (_ball != null) {
				_ball.SetActive(shot.ball);
			}
			var sceneView = SceneView.lastActiveSceneView;
			var (look, position) = Aim(shot, _insert, _unlit);
			var size = (look - position).magnitude * Mathf.Sin(sceneView.camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
			sceneView.LookAt(look, Quaternion.LookRotation(look - position, Vector3.up), size, false, true);
			File.AppendAllText(Path.Combine(Folder, "frames.txt"), $"{shot.name}: look {look.ToString("F4")} from {position.ToString("F4")}\n");
			_passes = 0;
			SceneView.duringSceneGui -= OnSceneGui;
			SceneView.duringSceneGui += OnSceneGui;
			sceneView.Repaint();
		}

		/// <summary>Where a still looks at and from: the insert's middle, which its origin need not be, or between the two inserts, plus the still's offsets.</summary>
		private static (Vector3 look, Vector3 position) Aim(Shot shot, InsertComponent insert, InsertComponent unlit)
		{
			var center = insert.GetComponent<MeshRenderer>().bounds.center;
			if (shot.frameBoth && unlit != null) {
				center = (center + unlit.GetComponent<MeshRenderer>().bounds.center) * 0.5f;
			}
			var look = center + new Vector3(shot.lookOffset[0], shot.lookOffset[1], shot.lookOffset[2]);
			var position = center + new Vector3(shot.camOffset[0], shot.camOffset[1], shot.camOffset[2]);
			return (look, position);
		}

		private static void OnSceneGui(SceneView sceneView)
		{
			if (Event.current.type != EventType.Repaint) {
				return;
			}
			// Temporal effects settle over a few still frames after the camera jump.
			if (++_passes < 12) {
				sceneView.Repaint();
				return;
			}
			SceneView.duringSceneGui -= OnSceneGui;
			var shot = _config.shots[_shot];
			var cam = sceneView.camera;
			var rt = new RenderTexture(shot.width, shot.height, 24, RenderTextureFormat.ARGB32);
			var tex = new Texture2D(shot.width, shot.height, TextureFormat.RGB24, false);
			var previous = cam.targetTexture;
			try {
				cam.targetTexture = rt;
				cam.Render();
				cam.Render();
				RenderTexture.active = rt;
				tex.ReadPixels(new Rect(0, 0, shot.width, shot.height), 0, 0);
				tex.Apply();
				File.WriteAllBytes(Path.Combine(Folder, shot.name + ".png"), tex.EncodeToPNG());
			} finally {
				cam.targetTexture = previous;
				RenderTexture.active = null;
				Object.DestroyImmediate(tex);
				rt.Release();
				Object.DestroyImmediate(rt);
			}
			_shot++;
			EditorApplication.update += Next;
		}

		private static void Next()
		{
			EditorApplication.update -= Next;
			Begin();
		}

		private static void Finish()
		{
			for (var i = 0; i < LampVectors.Length; i++) {
				// A variant that followed its parent keeps following it.
				if (_lampInherited[i]) {
					_body.RevertPropertyOverride(LampVectors[i]);
				} else {
					_body.SetVector(LampVectors[i], _lampBackup[i]);
				}
			}
			RestoreMaterials();
			foreach (var (insert, cover, normals) in ComponentBackup) {
				if (insert.CoverMaterial != cover) {
					insert.SetCoverMaterial(cover);
				}
				if (insert.UsePlayfieldNormals != normals) {
					insert.UsePlayfieldNormals = normals;
				}
			}
			var b = _lightBackup;
			_light.Intensity = b.Intensity;
			_light.Output = b.Output;
			_light.Color = b.Color;
			_light.TintWithLamp = b.TintWithLamp;
			_light.LampTint = b.Tint;
			_light.SpillLumens = b.Lumens;
			_light.SpillRange = b.Range;
			var so = new SerializedObject(_light);
			var bulbs = so.FindProperty("ExtraBulbs");
			for (var i = 0; i < bulbs.arraySize && i < b.ExtraIntensity.Length; i++) {
				bulbs.GetArrayElementAtIndex(i).FindPropertyRelative("Intensity").floatValue = b.ExtraIntensity[i];
				bulbs.GetArrayElementAtIndex(i).FindPropertyRelative("Color").colorValue = b.ExtraColor[i];
			}
			so.ApplyModifiedPropertiesWithoutUndo();
			_light.Apply();
			if (_unlitLight != null) {
				_unlitLight.Intensity = _unlitIntensity;
				_unlitLight.Apply();
			}
			if (_ball != null) {
				Object.DestroyImmediate(_ball);
				_ball = null;
			}
			var sceneView = SceneView.lastActiveSceneView;
			sceneView.LookAt(_pivot, _rotation, _size, sceneView.orthographic, true);
			RepairLight();
			_running = false;
			AutomationShots.Release();
			File.WriteAllText(Path.Combine(Folder, "done.txt"), DateTime.Now.ToString("O"));
		}

		private static void SetMaterials(Shot shot)
		{
			RestoreMaterials();
			Set(_body, BodyBackup, shot.floats, shot.colors);
			if (_cover != null) {
				Set(_cover, CoverBackup, shot.coverFloats, shot.coverColors);
			}
		}

		private static void Set(Material material, Dictionary<string, MaterialValue> backup, string[] floats, string[] colors)
		{
			foreach (var entry in floats ?? new string[0]) {
				var parts = entry.Split('=');
				Remember(material, backup, parts[0], false);
				material.SetFloat(parts[0], P(parts[1]));
			}
			foreach (var entry in colors ?? new string[0]) {
				var parts = entry.Split('=');
				var v = parts[1].Split(',').Select(P).ToArray();
				Remember(material, backup, parts[0], true);
				material.SetColor(parts[0], new Color(v[0], v[1], v[2], 1f));
			}
		}

		private static void Remember(Material material, Dictionary<string, MaterialValue> backup, string name, bool color)
		{
			if (backup.ContainsKey(name)) {
				return;
			}
			backup[name] = new MaterialValue {
				IsColor = color,
				Value = color ? 0f : material.GetFloat(name),
				Color = color ? material.GetColor(name) : default,
				Overridden = material.parent == null || material.IsPropertyOverriden(name),
			};
		}

		private static void RestoreMaterials()
		{
			Restore(_body, BodyBackup);
			if (_cover != null) {
				Restore(_cover, CoverBackup);
			}
		}

		private static void Restore(Material material, Dictionary<string, MaterialValue> backup)
		{
			foreach (var (name, value) in backup) {
				// A variant that followed its parent keeps following it.
				if (!value.Overridden) {
					material.RevertPropertyOverride(name);
				} else if (value.IsColor) {
					material.SetColor(name, value.Color);
				} else {
					material.SetFloat(name, value.Value);
				}
			}
			backup.Clear();
		}

		private static void SetComponents(Shot shot)
		{
			foreach (var (insert, cover, normals) in ComponentBackup) {
				var wanted = shot.noCover ? null : cover;
				if (insert.CoverMaterial != wanted) {
					insert.SetCoverMaterial(wanted);
				}
				var use = shot.playfieldNormals < 0 ? normals : shot.playfieldNormals > 0;
				if (insert.UsePlayfieldNormals != use) {
					insert.UsePlayfieldNormals = use;
				}
			}
		}

		/// <summary>Leaves the Scene view where the first still of "shots.json" looks from, to frame it by hand.</summary>
		[MenuItem(Automation.Menu + "Lighting/Frame Scene View")]
		public static void FrameSceneView()
		{
			var config = LoadConfig();
			var insert = Find(config.insert);
			var unlit = string.IsNullOrEmpty(config.unlit) ? null : Find(config.unlit);
			var (look, position) = Aim(config.shots[0], insert, unlit);
			var sceneView = SceneView.lastActiveSceneView;
			var size = (look - position).magnitude * Mathf.Sin(sceneView.camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
			sceneView.LookAt(look, Quaternion.LookRotation(look - position, Vector3.up), size, false, false);
			sceneView.Repaint();
		}

		/// <summary>Writes the Scene view's camera and pivot into "sceneview.txt", as offsets from where the stills aim, to put into "shots.json".</summary>
		[MenuItem(Automation.Menu + "Lighting/Read Scene View")]
		public static void ReadSceneView()
		{
			var config = LoadConfig();
			var insert = Find(config.insert);
			var unlit = string.IsNullOrEmpty(config.unlit) ? null : Find(config.unlit);
			var center = insert.GetComponent<MeshRenderer>().bounds.center;
			if (unlit != null) {
				center = (center + unlit.GetComponent<MeshRenderer>().bounds.center) * 0.5f;
			}
			var sceneView = SceneView.lastActiveSceneView;
			var cam = sceneView.camera.transform.position - center;
			var look = sceneView.pivot - center;
			File.WriteAllText(Path.Combine(Folder, "sceneview.txt"), FormattableString.Invariant($"{cam.x:R} {cam.y:R} {cam.z:R} {look.x:R} {look.y:R} {look.z:R} {sceneView.camera.fieldOfView:R} {sceneView.orthographic}"));
		}

		/// <summary>
		/// Takes back what putting the Insert Lights back records as overrides on
		/// their prefab instances, when it holds the prefab's own value again, and
		/// the spill light's reference, should it be lost. Runs after a capture.
		/// </summary>
		[MenuItem(Automation.Menu + "Lighting/Repair Light")]
		public static void RepairLight()
		{
			var config = LoadConfig();
			var log = new StringBuilder();
			Repair(Find(config.insert), log);
			if (!string.IsNullOrEmpty(config.unlit)) {
				Repair(Find(config.unlit), log);
			}
			File.WriteAllText(Path.Combine(Folder, "repair.txt"), log.ToString());
		}

		private static void Repair(InsertComponent insert, StringBuilder log)
		{
			log.AppendLine($"== {insert.name}");
			var light = insert.GetComponent<InsertLightComponent>();
			if (light.SpillLight == null && PrefabUtility.IsPartOfPrefabInstance(light)) {
				PrefabUtility.RevertPropertyOverride(new SerializedObject(light).FindProperty("SpillLight"), InteractionMode.AutomatedAction);
				log.AppendLine("reverted SpillLight");
			}
			var source = PrefabUtility.GetCorrespondingObjectFromSource(light);
			if (source != null) {
				var mine = new SerializedObject(light);
				var theirs = new SerializedObject(source);
				foreach (var name in new[] { "Intensity", "Output", "Color", "TintWithLamp", "LampTint", "SpillLumens", "SpillRange" }) {
					var property = mine.FindProperty(name);
					if (property != null && property.prefabOverride && SerializedProperty.DataEquals(property, theirs.FindProperty(name))) {
						PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
						log.AppendLine($"reverted {name}, equal to the prefab");
					}
				}
				var mineBulbs = mine.FindProperty("ExtraBulbs");
				var theirBulbs = theirs.FindProperty("ExtraBulbs");
				for (var i = 0; i < mineBulbs.arraySize && i < theirBulbs.arraySize; i++) {
					var color = mineBulbs.GetArrayElementAtIndex(i).FindPropertyRelative("Color");
					if (color.prefabOverride && SerializedProperty.DataEquals(color, theirBulbs.GetArrayElementAtIndex(i).FindPropertyRelative("Color"))) {
						PrefabUtility.RevertPropertyOverride(color, InteractionMode.AutomatedAction);
						log.AppendLine($"reverted {color.propertyPath}, equal to the prefab");
					}
				}
				// The stills set the extra bulbs' intensities through the serialized object, which records them as
				// overrides; those back at the prefab's value go, an authored one stays.
				var bulbs = new SerializedObject(light).FindProperty("ExtraBulbs");
				for (var i = 0; i < bulbs.arraySize && i < theirBulbs.arraySize; i++) {
					var intensity = bulbs.GetArrayElementAtIndex(i).FindPropertyRelative("Intensity");
					if (intensity.prefabOverride && SerializedProperty.DataEquals(intensity, theirBulbs.GetArrayElementAtIndex(i).FindPropertyRelative("Intensity"))) {
						PrefabUtility.RevertPropertyOverride(intensity, InteractionMode.AutomatedAction);
						log.AppendLine($"reverted {intensity.propertyPath}");
					}
				}
			}
			light.Apply();
			log.AppendLine($"spill light {(light.SpillLight != null ? light.SpillLight.name : "none")}");
		}

		/// <summary>
		/// The insert's body material, or with "1" in "material-slot.txt" its
		/// cover, in pieces scrolled down a step at a time into
		/// "material-{offset}.png" or "cover-{offset}.png", at the display's own
		/// scale: HDRP lays its blocks out against the window.
		/// </summary>
		[MenuItem(Automation.Menu + "Lighting/Shoot Material")]
		public static void ShootMaterial()
		{
			if (AutomationShots.Refuse()) {
				return;
			}
			var folder = Folder;
			var insert = Find(LoadConfig().insert);
			var slotFile = Path.Combine(folder, "material-slot.txt");
			var slot = File.Exists(slotFile) ? int.Parse(File.ReadAllText(slotFile).Trim()) : 0;
			var material = insert.GetComponent<MeshRenderer>().sharedMaterials[slot];
			// A material's inspector draws only while its foldout is open, which each material remembers.
			InternalEditorUtility.SetIsInspectorExpanded(material, true);
			var prefix = slot == 0 ? "material" : "cover";
			AutomationShots.Run(folder, Enumerable.Range(0, 6)
				.Select(i => new AutomationShots.Shot($"{prefix}-{i * 500}", material, 340f, 560f, 1f, i * 500f)));
		}

		/// <summary>The insert's Clear Cover section into "inspector-cover-section.png", on an unpacked copy beside it.</summary>
		[MenuItem(Automation.Menu + "Lighting/Shoot Cover Section")]
		public static void ShootCoverSection()
		{
			if (AutomationShots.Refuse()) {
				return;
			}
			var folder = Folder;
			var insert = Find(LoadConfig().insert);
			foreach (var key in InsertSections) {
				SessionState.SetBool("VisualPinball.Insert." + key, key == "Cover");
			}
			var copy = UnpackedCopy(insert);
			AutomationShots.Run(folder, new[] { new AutomationShots.Shot("inspector-cover-section", copy.GetComponent<InsertComponent>(), 340f, 360f) }, () => {
				Object.DestroyImmediate(copy);
				foreach (var key in InsertSections) {
					SessionState.EraseBool("VisualPinball.Insert." + key);
				}
			});
		}

		/// <summary>The insert's Insert Light by section, lit, into "inspector-lamps.png", "inspector-state.png" and "inspector-spill.png", on an unpacked copy beside it.</summary>
		[MenuItem(Automation.Menu + "Lighting/Shoot Inspector")]
		public static void ShootInspector()
		{
			if (AutomationShots.Refuse()) {
				return;
			}
			var folder = Folder;
			var insert = Find(LoadConfig().insert);
			var copy = UnpackedCopy(insert);
			var light = copy.GetComponent<InsertLightComponent>();
			light.Intensity = 1f;
			light.Apply();
			var shots = new List<AutomationShots.Shot>();
			foreach (var (name, section) in new[] { ("inspector-lamps", "Lamp"), ("inspector-state", "State"), ("inspector-spill", "Spill") }) {
				shots.Add(AutomationShots.Shot.Then(() => {
					foreach (var key in LightSections) {
						SessionState.SetBool("VisualPinball.InsertLight." + key, key == section);
					}
				}));
				shots.Add(new AutomationShots.Shot(name, light, 340f, 520f));
			}
			AutomationShots.Run(folder, shots, () => {
				Object.DestroyImmediate(copy);
				foreach (var key in LightSections) {
					SessionState.EraseBool("VisualPinball.InsertLight." + key);
				}
			});
		}

		/// <summary>A copy of the insert beside it that isn't a prefab instance: an instance's overrides would show as bold labels and blue bars.</summary>
		private static GameObject UnpackedCopy(InsertComponent insert)
		{
			var copy = Object.Instantiate(insert.gameObject, insert.transform.parent);
			copy.name = insert.name;
			return copy;
		}
	}
}
