// The preset example stills of the inserts documentation (presets/*.png), which Pinball/Automation can't take yet:
// they apply lamp and color presets to an insert for good, and look presets for the still only. Copy this file into
// a table project's Assets/Editor, use it through shoot_presets.sh, and delete it again.
//
// Input and output in <project>/Library/Automation/presets: target.txt (what the insert's name contains), lamp.txt,
// color.txt and capture.json in; <shot>.png, cams.txt and done.txt out.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity;
using VisualPinball.Unity.Editor;

public static class PresetStills
{
	private static string Folder
	{
		get {
			var folder = Path.GetFullPath(Path.Combine("Library", "Automation", "presets"));
			Directory.CreateDirectory(folder);
			return folder;
		}
	}

	private static string Target => File.ReadAllText(Path.Combine(Folder, "target.txt")).Trim();

	private static InsertComponent FindInsert() => UnityEngine.Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None)
		.First(i => i.name.Contains(Target));

	/// <summary>Applies the lamp preset named in lamp.txt to the target's light, for good.</summary>
	[MenuItem("Automation Stills/Presets/Apply Lamp")]
	public static void ApplyLamp()
	{
		var name = File.ReadAllText(Path.Combine(Folder, "lamp.txt")).Trim();
		var preset = InsertLampPresets.Find().First(p => p.LampName == name);
		var light = FindInsert().GetComponent<InsertLightComponent>();
		InsertLampPresets.Apply(preset, new[] { light });
		File.WriteAllText(Path.Combine(Folder, "lamp-done.txt"), $"{light.name} {preset.LampName} output {light.Output}");
	}

	/// <summary>Applies the color preset named in color.txt to the target, for good: it then wears that color's library materials.</summary>
	[MenuItem("Automation Stills/Presets/Apply Color")]
	public static void ApplyColor()
	{
		var name = File.ReadAllText(Path.Combine(Folder, "color.txt")).Trim();
		var preset = InsertColorPresets.Find().First(p => p.ColorName == name);
		var insert = FindInsert();
		var ok = InsertColorPresets.Apply(preset, insert);
		File.WriteAllText(Path.Combine(Folder, "color-done.txt"), $"{insert.name} {name} {ok} -> {insert.GetComponent<MeshRenderer>().sharedMaterial.name} / {insert.CoverMaterial?.name}");
	}

	[Serializable]
	private class Shot
	{
		public string name;
		/// <summary>The light's intensity and output, -1 for as they are.</summary>
		public float intensity = -1f;
		public float output = -1f;
		/// <summary>A look preset whose float entries, but Max Brightness, go on the body material for the still.</summary>
		public string look;
		/// <summary>The camera, relative to the insert's origin, looking at it.</summary>
		public float[] camOffset = { 0f, 0.05f, -0.004f };
		public int width = 900, height = 900;
	}

	[Serializable]
	private class Config
	{
		public Shot[] shots;
	}

	private static readonly Dictionary<string, (float value, bool overridden)> Backup = new();
	private static Config _config;
	private static int _shot;
	private static int _passes;
	private static InsertComponent _insert;
	private static Material _body;
	private static InsertLightComponent _light;
	private static float _intensity, _output;
	private static Vector3 _pivot;
	private static Quaternion _rotation;
	private static float _size;

	/// <summary>Renders every shot of capture.json through the Scene view.</summary>
	[MenuItem("Automation Stills/Presets/Capture")]
	public static void Capture()
	{
		_insert = FindInsert();
		_config = JsonUtility.FromJson<Config>(File.ReadAllText(Path.Combine(Folder, "capture.json")));
		_body = _insert.GetComponent<MeshRenderer>().sharedMaterials[0];
		_light = _insert.GetComponent<InsertLightComponent>();
		_intensity = _light.Intensity;
		_output = _light.Output;
		var sceneView = SceneView.lastActiveSceneView;
		_pivot = sceneView.pivot;
		_rotation = sceneView.rotation;
		_size = sceneView.size;
		File.Delete(Path.Combine(Folder, "done.txt"));
		File.WriteAllText(Path.Combine(Folder, "cams.txt"), "");
		_shot = 0;
		Begin();
	}

	private static void Restore()
	{
		foreach (var (name, (value, overridden)) in Backup) {
			// A variant that followed its parent keeps following it.
			if (_body.parent != null && !overridden) {
				_body.RevertPropertyOverride(name);
			} else {
				_body.SetFloat(name, value);
			}
		}
		Backup.Clear();
	}

	private static void Begin()
	{
		if (_shot >= _config.shots.Length) {
			Finish();
			return;
		}
		var shot = _config.shots[_shot];
		Restore();
		if (!string.IsNullOrEmpty(shot.look)) {
			var preset = InsertMaterialPresets.Find().First(p => p.name == shot.look);
			foreach (var entry in preset.Body.Where(e => e.Kind == InsertMaterialPreset.Kind.Float && _body.HasProperty(e.Name) && e.Name != "_InsertLuminosity")) {
				if (!Backup.ContainsKey(entry.Name)) {
					Backup[entry.Name] = (_body.GetFloat(entry.Name), _body.parent == null || _body.IsPropertyOverriden(entry.Name));
				}
				_body.SetFloat(entry.Name, entry.Float);
			}
		}
		_light.Intensity = shot.intensity >= 0f ? shot.intensity : _intensity;
		_light.Output = shot.output >= 0f ? shot.output : _output;
		_light.Apply();
		var sceneView = SceneView.lastActiveSceneView;
		var look = _insert.transform.position;
		var position = look + new Vector3(shot.camOffset[0], shot.camOffset[1], shot.camOffset[2]);
		var size = (look - position).magnitude * Mathf.Sin(sceneView.camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
		sceneView.LookAt(look, Quaternion.LookRotation(look - position, Vector3.up), size, false, true);
		_passes = 0;
		SceneView.duringSceneGui -= OnSceneGui;
		SceneView.duringSceneGui += OnSceneGui;
		sceneView.Repaint();
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
			UnityEngine.Object.DestroyImmediate(tex);
			rt.Release();
			UnityEngine.Object.DestroyImmediate(rt);
		}
		File.AppendAllText(Path.Combine(Folder, "cams.txt"), $"{shot.name} cam {cam.transform.position.ToString("R", CultureInfo.InvariantCulture)} fov {cam.fieldOfView}\n");
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
		Restore();
		_light.Intensity = _intensity;
		_light.Output = _output;
		_light.Apply();
		var sceneView = SceneView.lastActiveSceneView;
		sceneView.LookAt(_pivot, _rotation, _size, sceneView.orthographic, true);
		File.WriteAllText(Path.Combine(Folder, "done.txt"), DateTime.Now.ToString("O"));
	}
}
