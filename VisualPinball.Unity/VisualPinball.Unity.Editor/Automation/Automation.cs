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
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// What the automation menus share: the folder each of them reads its
	/// input from and writes its output to, and the capture of an inspector
	/// into a PNG. They take the screenshots of the documentation, driven
	/// from outside the editor through its menus.
	/// </summary>
	public static class Automation
	{
		public const string Menu = "Pinball/Automation/";

		/// <summary>The editor preference that moves the output somewhere else.</summary>
		private const string OutputKey = "VisualPinball.Automation.Output";

		/// <summary>
		/// The folder of one automation, created if needed: under the project's
		/// Library folder, which version control leaves out, unless the editor
		/// preference says otherwise.
		/// </summary>
		public static string Folder(string name)
		{
			var root = EditorPrefs.GetString(OutputKey, Path.GetFullPath(Path.Combine("Library", "Automation")));
			var folder = Path.Combine(root, name);
			Directory.CreateDirectory(folder);
			return folder;
		}

		/// <summary>Reads a file the automation takes its input from, trimmed.</summary>
		public static string Input(string folder, string file) => File.ReadAllText(Path.Combine(folder, file)).Trim();

		/// <summary>Writes what the window shows into a PNG, the right way up.</summary>
		public static void Capture(EditorWindow window, string path)
		{
			var ppp = EditorGUIUtility.pixelsPerPoint;
			var width = (int)(window.position.width * ppp);
			var height = (int)(window.position.height * ppp);
			var parent = typeof(EditorWindow).GetField("m_Parent", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(window);
			var grab = parent?.GetType().GetMethod("GrabPixels", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
			if (grab == null) {
				throw new InvalidOperationException("This version of Unity can't grab a window's pixels.");
			}
			var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
			var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
			var previous = RenderTexture.active;
			try {
				grab.Invoke(parent, new object[] { rt, new Rect(0f, 0f, width, height) });
				RenderTexture.active = rt;
				tex.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
				// The grab comes bottom up.
				var pixels = tex.GetPixels32();
				var flipped = new Color32[pixels.Length];
				for (var y = 0; y < height; y++) {
					Array.Copy(pixels, y * width, flipped, (height - 1 - y) * width, width);
				}
				tex.SetPixels32(flipped);
				tex.Apply();
				File.WriteAllBytes(path, tex.EncodeToPNG());
			} finally {
				RenderTexture.active = previous;
				rt.Release();
				Object.DestroyImmediate(rt);
				Object.DestroyImmediate(tex);
			}
		}
	}

	/// <summary>
	/// A window that shows one object's inspector, or another window's GUI,
	/// at a scale and scrolled, for a screenshot: Unity's own windows can be
	/// neither.
	/// </summary>
	public class AutomationShotWindow : EditorWindow
	{
		public Object Target;
		/// <summary>A window drawn inside this one instead of an inspector, kept out of sight and closed with it.</summary>
		public EditorWindow Guest;
		public float Scale = 2f;
		/// <summary>How far down the inspector the window starts, in points.</summary>
		public float Offset;
		/// <summary>Whether the component's title bar, icon and all, is shown above it, as the Inspector shows it.</summary>
		public bool Titlebar;

		private UnityEditor.Editor _editor;

		private void OnGUI()
		{
			GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
			GUILayout.BeginArea(new Rect(0f, -Offset, position.width / Scale, position.height / Scale + Offset));
			try {
				if (Guest != null) {
					Guest.GetType().GetMethod("OnGUI", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)?.Invoke(Guest, null);
				} else if (Target != null) {
					if (_editor == null || _editor.target != Target) {
						_editor = UnityEditor.Editor.CreateEditor(Target);
					}
					if (Titlebar) {
						EditorGUILayout.InspectorTitlebar(true, _editor);
					}
					_editor.OnInspectorGUI();
				}
			} finally {
				GUILayout.EndArea();
				GUI.matrix = Matrix4x4.identity;
			}
		}

		private void OnDisable()
		{
			if (_editor != null) {
				DestroyImmediate(_editor);
			}
			if (Guest != null) {
				Guest.Close();
			}
		}

		/// <summary>
		/// Opens the guest window out of sight, at the given size in points, and
		/// a shot window drawing it at the scale.
		/// </summary>
		public static AutomationShotWindow Host(EditorWindow guest, string title, float width, float height, float scale = 2f)
		{
			guest.ShowUtility();
			guest.position = new Rect(-4000f, -4000f, width, height);
			var host = CreateInstance<AutomationShotWindow>();
			host.Guest = guest;
			host.Scale = scale;
			host.titleContent = new GUIContent(title);
			host.ShowUtility();
			host.position = new Rect(20f, 60f, width * scale, height * scale);
			return host;
		}
	}

	/// <summary>
	/// Shots of inspectors, one after the other: each opens a window, waits
	/// for the inspector to settle and captures it into a PNG named after it.
	/// A file "done.txt" in the folder says when the last one is written.
	/// </summary>
	public static class AutomationShots
	{
		public readonly struct Shot
		{
			public readonly string Name;
			public readonly Object Target;
			public readonly float Width;
			public readonly float Height;
			public readonly float Scale;
			public readonly float Offset;
			public readonly bool Titlebar;
			/// <summary>Opens the window to shoot instead of an inspector, when set.</summary>
			public readonly Func<EditorWindow> Open;
			/// <summary>Something to do between two shots instead of a shot, when set.</summary>
			public readonly Action Do;

			/// <param name="name">The PNG's file name, without extension</param>
			/// <param name="target">The object whose inspector is shot</param>
			/// <param name="width">The window's width, in points before scaling</param>
			/// <param name="height">The window's height, in points before scaling</param>
			/// <param name="scale">How much larger than the Inspector the window draws</param>
			/// <param name="offset">How far down the inspector the window starts, in points</param>
			/// <param name="titlebar">Whether the component's title bar is shown above it</param>
			public Shot(string name, Object target, float width, float height, float scale = 2f, float offset = 0f, bool titlebar = false)
			{
				Name = name;
				Target = target;
				Width = width;
				Height = height;
				Scale = scale;
				Offset = offset;
				Titlebar = titlebar;
				Open = null;
				Do = null;
			}

			private Shot(string name, Func<EditorWindow> open, Action action)
			{
				Name = name;
				Target = null;
				Width = Height = Scale = Offset = 0f;
				Titlebar = false;
				Open = open;
				Do = action;
			}

			/// <summary>A shot of the window the function opens, closed after it.</summary>
			public static Shot Window(string name, Func<EditorWindow> open) => new(name, open, null);

			/// <summary>Not a shot: something done between two of them, like putting back what a shot changed.</summary>
			public static Shot Then(Action action) => new(null, null, action);
		}

		// Frames the inspector gets to lay out, and the one it gets the focus again
		// on, before the capture: a fresh window draws its first frames unfinished.
		private const int SettleFrames = 25;
		private const int FocusFrame = 20;

		private static Queue<Shot> _shots;
		private static string _folder;
		private static Action _done;
		private static EditorWindow _window;
		private static string _name;
		private static int _frames;

		// One run at a time, of these shots or of the lighting capture: each backs up
		// what it changes, and a second one would back up the first one's changes.
		private static bool _busy;

		/// <summary>
		/// Whether a run is still going, said in the console: a menu checks it
		/// before it changes anything.
		/// </summary>
		public static bool Refuse()
		{
			if (_busy) {
				Debug.LogWarning("Automation: another run is still going; wait for its done.txt.");
			}
			return _busy;
		}

		/// <summary>Marks a run as going, for a runner of its own like the lighting capture.</summary>
		internal static void Acquire()
		{
			if (_busy) {
				throw new InvalidOperationException("Automation: a run is still going.");
			}
			_busy = true;
		}

		internal static void Release() => _busy = false;

		/// <summary>Takes the shots into the folder, then calls back.</summary>
		public static void Run(string folder, IEnumerable<Shot> shots, Action done = null)
		{
			Acquire();
			_shots = new Queue<Shot>(shots);
			_folder = folder;
			_done = done;
			File.Delete(Path.Combine(folder, "done.txt"));
			EditorApplication.update -= Step;
			EditorApplication.update += Step;
		}

		private static EditorWindow Inspector(Shot shot)
		{
			var window = ScriptableObject.CreateInstance<AutomationShotWindow>();
			window.Target = shot.Target;
			window.Scale = shot.Scale;
			window.Offset = shot.Offset;
			window.Titlebar = shot.Titlebar;
			window.titleContent = new GUIContent("Inspector");
			window.ShowUtility();
			window.position = new Rect(20f, 60f, shot.Width * shot.Scale, shot.Height * shot.Scale);
			return window;
		}

		private static void Step()
		{
			if (_window == null) {
				if (_shots.Count == 0) {
					EditorApplication.update -= Step;
					try {
						_done?.Invoke();
					} finally {
						_done = null;
						Release();
						File.WriteAllText(Path.Combine(_folder, "done.txt"), "done");
					}
					return;
				}
				var shot = _shots.Dequeue();
				if (shot.Do != null) {
					shot.Do();
					return;
				}
				_name = shot.Name;
				_frames = 0;
				_window = shot.Open != null ? shot.Open() : Inspector(shot);
				_window.Focus();
				return;
			}
			_window.Repaint();
			if (_frames == FocusFrame) {
				_window.Focus();
			}
			if (++_frames < SettleFrames) {
				return;
			}
			try {
				Automation.Capture(_window, Path.Combine(_folder, _name + ".png"));
			} finally {
				_window.Close();
				_window = null;
			}
		}
	}
}
