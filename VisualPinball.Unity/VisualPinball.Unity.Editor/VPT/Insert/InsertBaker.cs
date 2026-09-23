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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Bakes an insert in Blender: optimized UVs and a normal map, brought back
	/// into the project as an <see cref="InsertBakeData"/> with its texture and
	/// material, under <c>Assets/Inserts</c>.
	/// </summary>
	/// <remarks>
	/// Blender 5 runs in the background as a child process, one bake at a time,
	/// while a progress bar keeps the editor from changing the insert under it.
	/// Everything the bake needs ships with the package, in <c>Blender~</c>,
	/// which Unity leaves alone; UVPackMaster is used when Blender has it.
	/// </remarks>
	public static class InsertBaker
	{
		private const string BlenderPathKey = "VisualPinball.Insert.BlenderPath";
		private const string Script = "VisualPinball.Unity/VisualPinball.Unity.Editor/VPT/Insert/Blender~/bake_insert.py";
		private const string OutputRoot = "Assets/Inserts";
		private const string LogPrefix = "[bake_insert] ";

		#region Blender

		/// <summary>
		/// The Blender executable: the one set in the preferences, or else the
		/// newest Blender 5 that can be found. Null when there is none.
		/// </summary>
		public static string BlenderPath
		{
			get {
				var path = EditorPrefs.GetString(BlenderPathKey, "");
				if (!string.IsNullOrEmpty(path)) {
					return path;
				}
				// The inspector asks on every repaint, so the disk is searched once.
				_found ??= FindBlender() ?? "";
				return _found.Length > 0 ? _found : null;
			}
			set => EditorPrefs.SetString(BlenderPathKey, value ?? "");
		}

		private static string _found;

		private static string FindBlender()
		{
			if (Application.platform == RuntimePlatform.WindowsEditor) {
				var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Blender Foundation");
				if (!Directory.Exists(root)) {
					return null;
				}
				return Directory.GetDirectories(root, "Blender 5*")
					.Select(directory => (Directory: directory, Version: Version.TryParse(Path.GetFileName(directory)[8..], out var v) ? v : new Version()))
					.OrderByDescending(found => found.Version)
					.Select(found => Path.Combine(found.Directory, "blender.exe"))
					.FirstOrDefault(File.Exists);
			}
			if (Application.platform == RuntimePlatform.OSXEditor) {
				const string app = "/Applications/Blender.app/Contents/MacOS/Blender";
				return File.Exists(app) ? app : null;
			}
			return (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
				.Select(directory => Path.Combine(directory, "blender"))
				.FirstOrDefault(File.Exists);
		}

		/// <summary>What a Blender executable has installed, as its probe reported it.</summary>
		[Serializable]
		public class BlenderInfo
		{
			public string Blender;
			public int Version;
			public bool Supported;
			public bool UvPackMaster;

			/// <summary>Why the probe failed, or null.</summary>
			[NonSerialized] public string Error;
		}

		private static string _probedPath;
		private static BlenderInfo _probe;
		private static Process _probing;
		private static readonly ConcurrentQueue<string> ProbeOutput = new();

		/// <summary>
		/// What the current Blender has installed, or null while that is still
		/// being found out, which takes Blender a couple of seconds to start.
		/// </summary>
		public static BlenderInfo Probe()
		{
			// Never null, so a missing Blender is not mistaken for one being probed.
			var path = BlenderPath ?? "";
			if (path == _probedPath) {
				return _probe;
			}
			if (_probing != null) {
				return null;
			}
			if (path.Length == 0 || !File.Exists(path)) {
				_probedPath = path;
				_probe = new BlenderInfo { Error = "Blender 5 was not found. Set its path above." };
				return _probe;
			}
			try {
				_probing = Run(path, "--probe", ProbeOutput);
				EditorApplication.update += FinishProbe;
			} catch (Exception exception) {
				_probing = null;
				_probedPath = path;
				_probe = new BlenderInfo { Error = $"Blender could not be started: {exception.Message}" };
				return _probe;
			}
			_probedPath = null;
			return null;
		}

		private static void FinishProbe()
		{
			if (!_probing.HasExited) {
				return;
			}
			EditorApplication.update -= FinishProbe;
			_probing.WaitForExit();
			var path = _probing.StartInfo.FileName;
			_probing.Dispose();
			_probing = null;

			var line = Drain(ProbeOutput).LastOrDefault(l => l.StartsWith(LogPrefix + "{"));
			var info = line != null ? JsonUtility.FromJson<BlenderInfo>(line[LogPrefix.Length..]) : null;
			if (info == null) {
				info = new BlenderInfo { Error = "Blender did not run the bake script. Is this a Blender executable?" };
			} else if (info.Version != InsertBakeRequest.CurrentVersion) {
				info.Error = $"The bake script is version {info.Version}, this editor writes version {InsertBakeRequest.CurrentVersion}.";
			} else if (!info.Supported) {
				info.Error = $"Blender {info.Blender} is too old. The bake needs Blender 5.0 or newer.";
			}
			_probedPath = path;
			_probe = info;
			UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
		}

		private static Process Run(string blender, string argument, ConcurrentQueue<string> output)
		{
			// Blender~ is no asset, so it is found through the package's own folder.
			var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(InsertBaker).Assembly);
			var script = Path.Combine(package?.resolvedPath ?? "", Script);
			if (!File.Exists(script)) {
				throw new FileNotFoundException($"The bake script is missing at {script}.");
			}
			var process = new Process {
				StartInfo = new ProcessStartInfo(blender, $"-b --python \"{script}\" -- {argument}") {
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true,
				},
				EnableRaisingEvents = true,
			};
			process.OutputDataReceived += (_, e) => { if (e.Data != null) output.Enqueue(e.Data); };
			process.ErrorDataReceived += (_, e) => { if (e.Data != null) output.Enqueue(e.Data); };
			process.Start();
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();
			return process;
		}

		private static List<string> Drain(ConcurrentQueue<string> queue)
		{
			var lines = new List<string>();
			while (queue.TryDequeue(out var line)) {
				lines.Add(line);
			}
			return lines;
		}

		#endregion

		#region Baking

		private sealed class Job
		{
			public InsertComponent Insert;
			public string Name;
			public string Folder;
			public string Hash;
			public int TextureSize;
			public Process Process;
			public readonly ConcurrentQueue<string> Output = new();
			public readonly List<string> Log = new();
			public string Status = "Optimizing UVs";
			public float Progress = 0.1f;
		}

		private static Job _job;

		/// <summary>The insert being baked, or null.</summary>
		public static InsertComponent Baking => _job?.Insert;

		/// <summary>Blender's output of the last bake, for when it went wrong.</summary>
		public static IReadOnlyList<string> LastLog { get; private set; } = Array.Empty<string>();

		/// <summary>Why the last bake failed, or null.</summary>
		public static string LastError { get; private set; }

		/// <summary>Which insert the last error belongs to.</summary>
		public static InsertComponent LastErrorInsert { get; private set; }

		/// <summary>How far Blender has got, by the steps the script announces.</summary>
		private static readonly (string Message, float Progress)[] Steps = {
			("Baking normals onto the regular mesh", 0.3f),
			("Baking normals onto the flat mesh", 0.6f),
			("Baking the flat region mask", 0.8f),
			("Saving the normal map", 0.9f),
		};

		public static void Bake(InsertComponent insert)
		{
			if (_job != null) {
				return;
			}
			LastError = null;
			LastErrorInsert = null;
			var blender = BlenderPath;
			var info = Probe();
			if (info?.Error != null) {
				Fail(insert, info.Error);
				return;
			}

			var mesh = new Mesh();
			try {
				var layout = new InsertMeshLayout();
				var body = insert.Generate(mesh, layout);
				var request = InsertBakeRequest.Build(mesh, layout, insert.BakeSettings, body.StarburstOffset);

				var folder = Path.GetFullPath(Path.Combine("Library", "InsertBake", Guid.NewGuid().ToString("N")));
				Directory.CreateDirectory(folder);
				var requestPath = Path.Combine(folder, "request.json");
				File.WriteAllText(requestPath, JsonUtility.ToJson(request));

				var job = new Job {
					Insert = insert,
					Name = insert.name,
					Folder = folder,
					Hash = layout.Hash.ToString(),
					TextureSize = request.TextureSize,
				};
				job.Process = Run(blender, $"\"{requestPath}\"", job.Output);
				_job = job;

				// A reload would drop this bake halfway, and Blender's result with it.
				EditorApplication.LockReloadAssemblies();
				EditorApplication.update += Update;

			} catch (Exception exception) {
				Fail(insert, exception.Message);

			} finally {
				Object.DestroyImmediate(mesh);
			}
		}

		private static void Update()
		{
			var job = _job;
			try {
				Step(job);
			} catch (Exception exception) {
				if (_job == job) {
					try {
						job.Process.Kill();
					} catch (InvalidOperationException) {
						// It had finished anyway.
					}
					End(job);
				}
				Fail(job.Insert, exception.Message);
				Debug.LogException(exception);
			}
		}

		private static void Step(Job job)
		{
			foreach (var line in Drain(job.Output)) {
				job.Log.Add(line);
				if (line.StartsWith(LogPrefix) && !line.StartsWith(LogPrefix + "{")) {
					var message = line[LogPrefix.Length..];
					job.Status = message;
					foreach (var step in Steps) {
						if (message.StartsWith(step.Message)) {
							job.Progress = step.Progress;
						}
					}
				}
			}

			if (EditorUtility.DisplayCancelableProgressBar($"Baking {job.Name}", job.Status, job.Progress)) {
				try {
					job.Process.Kill();
				} catch (InvalidOperationException) {
					// It had finished anyway.
				}
				End(job);
				Directory.Delete(job.Folder, true);
				return;
			}
			if (!job.Process.HasExited) {
				return;
			}
			job.Process.WaitForExit();
			job.Log.AddRange(Drain(job.Output));
			var exitCode = job.Process.ExitCode;
			End(job);
			Finish(job, exitCode);
			Directory.Delete(job.Folder, true);
		}

		private static void End(Job job)
		{
			EditorApplication.update -= Update;
			EditorUtility.ClearProgressBar();
			EditorApplication.UnlockReloadAssemblies();
			LastLog = job.Log.ToArray();
			job.Process.Dispose();
			_job = null;
		}

		[Serializable]
		private class BakeResult
		{
			public float[] Uvs;
			public float[] Tangents;
			public BakeReport Report;
		}

		[Serializable]
		private class BakeReport
		{
			public string Blender;
			public string Packer;
			public float UvFill;
			public string[] Warnings;
			public string Error;
		}

		private static void Finish(Job job, int exitCode)
		{
			var resultPath = Path.Combine(job.Folder, "result.json");
			if (!File.Exists(resultPath)) {
				throw new InvalidOperationException($"Blender stopped with code {exitCode} before it wrote a result. Its log has more.");
			}
			var result = JsonUtility.FromJson<BakeResult>(File.ReadAllText(resultPath));
			if (!string.IsNullOrEmpty(result.Report?.Error)) {
				throw new InvalidOperationException(result.Report.Error);
			}
			var insert = job.Insert;
			if (insert == null) {
				throw new InvalidOperationException("The insert was deleted while it was being baked.");
			}
			foreach (var warning in result.Report.Warnings ?? Array.Empty<string>()) {
				Debug.LogWarning($"{insert.name}: {warning}");
			}

			// A duplicated insert shares its original's bake until it bakes its own,
			// which must not overwrite the original's.
			var own = insert.Bake != null && !SharedWithAnotherInsert(insert) ? insert.Bake : null;
			var folder = own != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(own))?.Replace('\\', '/') : NewFolder(insert);
			var name = FileName(insert.name);

			// Not "... Normal.png": the asset library re-applies its own normal map
			// preset to every such file on import, wrapping included.
			var texturePath = own != null && own.NormalMap != null ? AssetDatabase.GetAssetPath(own.NormalMap) : $"{folder}/{name} Normal Map.png";
			File.Copy(Path.Combine(job.Folder, "normal.png"), texturePath, true);
			AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
			var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
			var textureSettings = new TextureImporterSettings();
			importer.ReadTextureSettings(textureSettings);
			textureSettings.textureType = TextureImporterType.NormalMap;
			// Charts stop short of the texture's edge, which must not wrap round
			// onto the far side when the map is filtered or mipmapped.
			textureSettings.wrapMode = TextureWrapMode.Clamp;
			importer.SetTextureSettings(textureSettings);
			importer.maxTextureSize = math.max(32, job.TextureSize);
			importer.SaveAndReimport();
			var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

			var data = own;
			var created = data == null;
			if (created) {
				data = ScriptableObject.CreateInstance<InsertBakeData>();
			}
			data.Hash = job.Hash;
			data.Uvs = new Vector2[result.Uvs.Length / 2];
			for (var i = 0; i < data.Uvs.Length; i++) {
				data.Uvs[i] = new Vector2(result.Uvs[i * 2], result.Uvs[i * 2 + 1]);
			}
			data.Tangents = new Vector4[result.Tangents.Length / 4];
			for (var i = 0; i < data.Tangents.Length; i++) {
				data.Tangents[i] = new Vector4(result.Tangents[i * 4], result.Tangents[i * 4 + 1], result.Tangents[i * 4 + 2], result.Tangents[i * 4 + 3]);
			}
			data.NormalMap = texture;
			data.Packer = result.Report.Packer;
			data.UvFill = result.Report.UvFill;
			data.Material = BakeMaterial(insert, own != null ? own.Material : null, $"{folder}/{name} Baked.mat", texture);
			if (created) {
				AssetDatabase.CreateAsset(data, $"{folder}/{name} Bake.asset");
			} else {
				EditorUtility.SetDirty(data);
			}
			AssetDatabase.SaveAssets();

			var renderer = insert.GetComponent<MeshRenderer>();
			Undo.RecordObjects(new Object[] { insert, renderer }, "Bake Insert");
			insert.SetBake(data);
			PrefabUtility.RecordPrefabInstancePropertyModifications(insert);
			PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
		}

		/// <summary>
		/// A copy of the material the insert wears unbaked, with the normal map
		/// set. The insert's own material is never touched.
		/// </summary>
		private static Material BakeMaterial(InsertComponent insert, Material existing, string path, Texture2D normalMap)
		{
			var source = insert.UnbakedMaterial;
			if (source == null) {
				source = new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
			}
			var material = existing;
			var created = material == null;
			if (created) {
				material = new Material(source);
			} else {
				material.shader = source.shader;
				material.CopyPropertiesFromMaterial(source);
			}

			if (material.HasTexture("_NormalMap")) {
				// HDRP's Lit family.
				material.SetTexture("_NormalMap", normalMap);
				material.SetFloat("_NormalMapSpace", 0f);
				material.EnableKeyword("_NORMALMAP");
				material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
			} else if (material.HasTexture("_BumpMap")) {
				material.SetTexture("_BumpMap", normalMap);
				material.EnableKeyword("_NORMALMAP");
			} else if (NormalMapSlot(material.shader) is { } slot) {
				// A shader graph's normal map input defaults to the flat "bump" texture.
				material.SetTexture(slot, normalMap);
			} else {
				Debug.LogWarning($"{insert.name}: the material's shader {source.shader.name} has no normal map slot; set {AssetDatabase.GetAssetPath(normalMap)} yourself.");
			}

			if (created) {
				material.name = Path.GetFileNameWithoutExtension(path);
				AssetDatabase.CreateAsset(material, path);
			} else {
				EditorUtility.SetDirty(material);
			}
			return material;
		}

		private static string NormalMapSlot(Shader shader)
		{
			for (var i = 0; i < shader.GetPropertyCount(); i++) {
				if (shader.GetPropertyType(i) != ShaderPropertyType.Texture) {
					continue;
				}
				if ((shader.GetPropertyFlags(i) & ShaderPropertyFlags.Normal) != 0 || shader.GetPropertyTextureDefaultName(i) == "bump") {
					return shader.GetPropertyName(i);
				}
			}
			return null;
		}

		/// <summary>
		/// Whether the insert's bake belongs to another insert as well: one in the
		/// open scenes, or its prefab, when it has the bake from there.
		/// </summary>
		private static bool SharedWithAnotherInsert(InsertComponent insert)
		{
			if (PrefabUtility.IsPartOfPrefabInstance(insert) && !new SerializedObject(insert).FindProperty("_bake").prefabOverride) {
				return true;
			}
			return Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include)
				.Any(other => other != insert && other.Bake == insert.Bake);
		}

		/// <summary>
		/// A folder of the insert's own for its first bake, named after it plus a
		/// few characters that keep two inserts of the same name apart.
		/// </summary>
		private static string NewFolder(InsertComponent insert)
		{
			if (!AssetDatabase.IsValidFolder(OutputRoot)) {
				AssetDatabase.CreateFolder("Assets", "Inserts");
			}
			var guid = AssetDatabase.CreateFolder(OutputRoot, $"{FileName(insert.name)} {Guid.NewGuid().ToString("N")[..8]}");
			return AssetDatabase.GUIDToAssetPath(guid);
		}

		private static string FileName(string name)
		{
			var clean = Regex.Replace(name, @"[<>:""/\\|?*\x00-\x1f]", "_").Trim().TrimEnd('.');
			return string.IsNullOrEmpty(clean) ? "Insert" : clean;
		}

		private static void Fail(InsertComponent insert, string error)
		{
			LastError = error;
			LastErrorInsert = insert;
			Debug.LogError($"Baking {(insert != null ? insert.name : "an insert")} failed: {error}");
		}

		#endregion
	}
}
