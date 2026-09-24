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
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
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
		public const string HybridShaderName = "Visual Pinball/Insert Hybrid";

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
			public bool HybridOptics;
			public Vector4 OpticalStarburst;
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
			("Baking internal scatter proxy", 0.92f),
			("Baking optical normals", 0.95f),
			("Baking optical response masks", 0.98f),
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
				// A hybrid material always needs maps from this exact UV layout.
				request.HybridOptics |= insert.Bake != null && insert.Bake.Material != null && insert.Bake.Material.HasProperty("_OpticalNormalMap");

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
					HybridOptics = request.HybridOptics,
					OpticalStarburst = layout.OpticalStarburst,
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
							job.Progress = math.max(job.Progress, step.Progress);
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
			public float[] OpticalLamp;
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
			if (job.HybridOptics && (result.OpticalLamp?.Length != 4 || !File.Exists(Path.Combine(job.Folder, "optical-normal.png")) || !File.Exists(Path.Combine(job.Folder, "optical-response.png")))) {
				throw new InvalidOperationException("Blender did not return the complete hybrid optics bake. The previous bake has been kept.");
			}
			var insert = job.Insert;
			if (insert == null) {
				throw new InvalidOperationException("The insert was deleted while it was being baked.");
			}
			foreach (var warning in result.Report.Warnings ?? Array.Empty<string>()) {
				Debug.LogWarning($"{insert.name}: {warning}");
			}

			// A duplicated insert shares its original's bake until it bakes its own,
			// which must not overwrite the original's. A bake that cannot be written
			// to, in a package, is never anyone's to overwrite.
			var own = Owns(insert, insert.Bake) && !IsReadOnly(insert.Bake) ? insert.Bake : null;
			var folder = own != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(own))?.Replace('\\', '/') : NewFolder(insert);
			var name = FileName(insert.name);
			// Whether the material the insert wears is this bake's own is judged
			// against the maps the bake had, before the new ones replace them.
			var renderer = insert.GetComponent<MeshRenderer>();
			var wornIsOwn = own != null && Carries(renderer.sharedMaterial, own);

			// Not "... Normal.png": the asset library re-applies its own normal map
			// preset to every such file on import, wrapping included.
			var texturePath = own != null && own.NormalMap != null && IsPng(own.NormalMap) ? AssetDatabase.GetAssetPath(own.NormalMap) : $"{folder}/{name} Normal Map.png";
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
			// Made sure of even when there was no bake to own: an id never seen
			// by OnEnable could still be a duplicate's or a prefab's.
			data.Owner = insert.EnsureId();
			data.Uvs = new Vector2[result.Uvs.Length / 2];
			for (var i = 0; i < data.Uvs.Length; i++) {
				data.Uvs[i] = new Vector2(result.Uvs[i * 2], result.Uvs[i * 2 + 1]);
			}
			data.Tangents = new Vector4[result.Tangents.Length / 4];
			for (var i = 0; i < data.Tangents.Length; i++) {
				data.Tangents[i] = new Vector4(result.Tangents[i * 4], result.Tangents[i * 4 + 1], result.Tangents[i * 4 + 2], result.Tangents[i * 4 + 3]);
			}
			data.NormalMap = texture;
			data.OpticalStarburst = job.OpticalStarburst;
			data.OpticalNormalMap = ImportOptics(job, folder, name, "optical-normal.png", "Optical Normals", data.OpticalNormalMap);
			data.OpticalResponseMap = ImportOptics(job, folder, name, "optical-response.png", "Optical Response", data.OpticalResponseMap);
			if (result.OpticalLamp?.Length == 4) {
				data.OpticalLamp = new Vector4(result.OpticalLamp[0], result.OpticalLamp[1], result.OpticalLamp[2], result.OpticalLamp[3]);
			}
			data.Packer = result.Report.Packer;
			data.UvFill = result.Report.UvFill;
			data.Material = BakeMaterial(insert, wornIsOwn, folder, name, texture);
			BindOptics(data);
			if (created) {
				AssetDatabase.CreateAsset(data, $"{folder}/{name} Bake.asset");
			} else {
				EditorUtility.SetDirty(data);
			}
			AssetDatabase.SaveAssets();

			Undo.RecordObjects(new Object[] { insert, renderer }, "Bake Insert");
			insert.SetBake(data);
			PrefabUtility.RecordPrefabInstancePropertyModifications(insert);
			PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
		}

		private static Texture2D ImportOptics(Job job, string folder, string name, string source, string suffix, Texture2D existing)
		{
			var sourcePath = Path.Combine(job.Folder, source);
			if (!File.Exists(sourcePath)) {
				return null;
			}
			var path = existing != null && IsPng(existing) ? AssetDatabase.GetAssetPath(existing) : $"{folder}/{name} {suffix}.png";
			File.Copy(sourcePath, path, true);
			AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
			var importer = (TextureImporter)AssetImporter.GetAtPath(path);
			importer.textureType = TextureImporterType.Default;
			importer.sRGBTexture = false;
			importer.isReadable = false;
			importer.wrapMode = TextureWrapMode.Clamp;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.maxTextureSize = math.max(32, job.TextureSize);
			importer.SaveAndReimport();
			return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		}

		/// <summary>Whether the texture is a PNG the bake can write over, rather than some other asset it was given.</summary>
		private static bool IsPng(Texture2D texture)
			=> string.Equals(Path.GetExtension(AssetDatabase.GetAssetPath(texture)), ".png", StringComparison.OrdinalIgnoreCase);

		private static void BindOptics(InsertBakeData data)
		{
			if (data.Material == null || !data.Material.HasProperty("_OpticalNormalMap")) {
				return;
			}
			data.Material.SetTexture("_OpticalNormalMap", data.OpticalNormalMap);
			data.Material.SetTexture("_OpticalResponseMap", data.OpticalResponseMap);
			if (data.Material.HasProperty("_OpticalStarburst")) {
				data.Material.SetVector("_OpticalStarburst", data.OpticalStarburst);
			}
			EditorUtility.SetDirty(data.Material);
		}

		/// <summary>Explicitly opt into the HDRP hybrid, keeping the previous material asset intact.</summary>
		public static void UseHybridMaterial(InsertComponent insert)
		{
			var shader = Shader.Find(HybridShaderName);
			var data = insert.Bake;
			if (shader == null || data == null || !insert.IsBakeFresh || data.OpticalNormalMap == null || data.OpticalResponseMap == null) {
				throw new InvalidOperationException("Bake Hybrid Optics first, with the HDRP package installed.");
			}
			if (IsReadOnly(data)) {
				throw new InvalidOperationException($"This bake belongs to {ReadOnlyReason(data)} and cannot be written to. Fork it to the project first.");
			}
			if (!Owns(insert, data)) {
				throw new InvalidOperationException("This bake is not this insert's own. Bake this insert again before choosing its own material.");
			}
			var renderer = insert.GetComponent<MeshRenderer>();
			Undo.RecordObjects(new Object[] { data, renderer }, "Use Hybrid Insert Material");
			// A bake from before ids is claimed on the way.
			data.Owner = insert.Id;
			var folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(data))?.Replace('\\', '/');
			var material = new Material(shader) { name = $"{insert.name} Hybrid" };
			material.SetTexture("_NormalMap", data.NormalMap);
			material.SetVector("_OpticalLamp", data.OpticalLamp);
			AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{FileName(insert.name)} Hybrid.mat"));
			data.Material = material;
			BindOptics(data);
			renderer.sharedMaterial = material;
			EditorUtility.SetDirty(data);
			PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
			AssetDatabase.SaveAssets();
		}

		/// <summary>
		/// The material the bake goes into, with the new normal map set: the one
		/// the insert wears when that is the bake's own (<paramref name="wornIsOwn"/>:
		/// the material the bake made or one carrying its maps, so a colour given
		/// to it, or a copy made of it with the maps kept, stays on) and no other
		/// insert wears it. Otherwise a copy of what the insert wears, a variant
		/// when that is a package's, so a material other inserts share is never
		/// written into and the insert still keeps its look.
		/// </summary>
		private static Material BakeMaterial(InsertComponent insert, bool wornIsOwn, string folder, string name, Texture2D normalMap)
		{
			var worn = insert.GetComponent<MeshRenderer>().sharedMaterial;
			var material = worn;
			var created = worn == null || !wornIsOwn || !EditorUtility.IsPersistent(worn) || IsReadOnly(AssetDatabase.GetAssetPath(worn));
			if (!created && WornElsewhere(worn, insert)) {
				created = true;
				Debug.Log($"{insert.name}: another insert wears {worn.name} too, so this bake goes into a copy of it.");
			}
			if (created) {
				material = worn != null ? Derive(worn) : new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
				material.name = worn != null ? $"{name} {worn.name}" : $"{name} Baked";
			} else {
				Undo.RecordObject(material, "Bake Insert");
			}

			SetNormalMap(material, normalMap, insert.name);

			if (created) {
				AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{FileName(material.name)}.mat"));
			} else {
				EditorUtility.SetDirty(material);
			}
			return material;
		}

		/// <summary>
		/// Whether another insert in the open scenes, or the prefab being edited,
		/// wears the material: a bake written into it would break that insert.
		/// </summary>
		private static bool WornElsewhere(Material material, InsertComponent insert)
		{
			foreach (var other in Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
				if (other != insert && other.TryGetComponent<MeshRenderer>(out var renderer) && renderer.sharedMaterial == material) {
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Whether the material is the bake's own: the one the bake made, or any
		/// other carrying one of its maps, such as a copy made for another colour.
		/// </summary>
		public static bool Carries(Material material, InsertBakeData bake)
		{
			if (material == null || bake == null) {
				return false;
			}
			if (material == bake.Material) {
				return true;
			}
			var shader = material.shader;
			for (var i = 0; i < shader.GetPropertyCount(); i++) {
				if (shader.GetPropertyType(i) != ShaderPropertyType.Texture) {
					continue;
				}
				var texture = material.GetTexture(shader.GetPropertyName(i));
				if (texture != null && (texture == bake.NormalMap || texture == bake.OpticalNormalMap || texture == bake.OpticalResponseMap)) {
					return true;
				}
			}
			return false;
		}

		private static void SetNormalMap(Material material, Texture2D normalMap, string insertName)
		{
			if (material.HasTexture("_NormalMap")) {
				// HDRP's Lit family.
				material.SetTexture("_NormalMap", normalMap);
				if (material.HasProperty("_NormalMapSpace")) {
					material.SetFloat("_NormalMapSpace", 0f);
					material.EnableKeyword("_NORMALMAP");
					material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
				}
			} else if (material.HasTexture("_BumpMap")) {
				material.SetTexture("_BumpMap", normalMap);
				material.EnableKeyword("_NORMALMAP");
			} else if (NormalMapSlot(material.shader) is { } slot) {
				// A shader graph's normal map input defaults to the flat "bump" texture.
				material.SetTexture(slot, normalMap);
			} else {
				Debug.LogWarning($"{insertName}: the material's shader {material.shader.name} has no normal map slot; set {AssetDatabase.GetAssetPath(normalMap)} yourself.");
			}
		}

		/// <summary>
		/// A material of the insert's own, made from the one it wears: a variant
		/// when that one is an asset that cannot be written to, so its tuning
		/// keeps following the package's; a plain copy otherwise.
		/// </summary>
		private static Material Derive(Material source)
		{
			return IsReadOnly(AssetDatabase.GetAssetPath(source)) ? Variant(source, source.name) : new Material(source);
		}

		/// <summary>
		/// A variant of the material, inheriting everything: it starts as a copy,
		/// so nothing counts as overridden until it is set afterwards. A fresh
		/// material given a parent would keep the shader's defaults instead.
		/// </summary>
		private static Material Variant(Material source, string name) => new(source) { parent = source, name = name };

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

		#endregion

		#region Forking

		/// <summary>
		/// Whether the bake's assets cannot be written to: they live in a package
		/// Unity keeps immutable, or version control holds them.
		/// </summary>
		public static bool IsReadOnly(InsertBakeData bake) => bake != null && IsReadOnly(AssetDatabase.GetAssetPath(bake));

		public static bool IsReadOnly(string assetPath)
		{
			if (string.IsNullOrEmpty(assetPath)) {
				return false;
			}
			var package = PackageInfo.FindForAssetPath(assetPath);
			if (package != null && package.source != PackageSource.Embedded && package.source != PackageSource.Local) {
				return true;
			}
			return !AssetDatabase.IsOpenForEdit(assetPath, StatusQueryOptions.UseCachedIfPossible);
		}

		/// <summary>What keeps the bake read-only, for the inspector to say.</summary>
		public static string ReadOnlyReason(InsertBakeData bake)
		{
			var path = AssetDatabase.GetAssetPath(bake);
			var package = PackageInfo.FindForAssetPath(path);
			return package != null && package.source != PackageSource.Embedded && package.source != PackageSource.Local
				? $"the package {package.displayName}"
				: "version control";
		}

		/// <summary>
		/// Gives the insert a bake of its own in the project: copies of the maps
		/// and bake data it wears, bound into the material it wears when that is
		/// the project's own, or into a variant of it when that is a package's,
		/// so an insert from a package can be tuned and baked here while the
		/// package stays as it is. A cover material from a package gets a
		/// variant too. When the copy no longer fits the mesh, a bake follows at
		/// once.
		/// </summary>
		public static InsertBakeData Fork(InsertComponent insert)
		{
			var source = insert.Bake;
			if (source == null) {
				throw new InvalidOperationException("The insert has no bake to fork.");
			}
			var renderer = insert.GetComponent<MeshRenderer>();
			var folder = NewFolder(insert);
			var name = FileName(insert.name);

			InsertBakeData data;
			Material cover = null;
			try {
				data = Object.Instantiate(source);
				data.name = $"{name} Bake";
				data.Owner = insert.EnsureId();
				data.NormalMap = CopyTexture(source.NormalMap, $"{folder}/{name} Normal Map");
				data.OpticalNormalMap = CopyTexture(source.OpticalNormalMap, $"{folder}/{name} Optical Normals");
				data.OpticalResponseMap = CopyTexture(source.OpticalResponseMap, $"{folder}/{name} Optical Response");
				AssetDatabase.CreateAsset(data, $"{folder}/{name} Bake.asset");
				// The material the insert wears takes the copies: a colour of the
				// user's own carrying the source's maps in place, when it is the
				// project's; the source's own material, or a package's, as a
				// variant that keeps following its tuning, since the source is
				// left as it was.
				var worn = renderer.sharedMaterial != null ? renderer.sharedMaterial : source.Material;
				if (worn != null) {
					var own = worn != source.Material && Carries(worn, source) && EditorUtility.IsPersistent(worn)
						&& !IsReadOnly(AssetDatabase.GetAssetPath(worn)) && !WornElsewhere(worn, insert);
					var material = own ? worn : Variant(worn, $"{name} {worn.name}");
					if (own) {
						Undo.RecordObject(material, "Fork Insert Bake");
					}
					if (data.NormalMap != null) {
						SetNormalMap(material, data.NormalMap, insert.name);
					}
					if (own) {
						EditorUtility.SetDirty(material);
					} else {
						// Unique, because the body and the cover may share a name.
						AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name} {FileName(worn.name)}.mat"));
					}
					data.Material = material;
					BindOptics(data);
					EditorUtility.SetDirty(data);
				}
				if (insert.CoverMaterial != null && IsReadOnly(AssetDatabase.GetAssetPath(insert.CoverMaterial))) {
					cover = Variant(insert.CoverMaterial, $"{name} {insert.CoverMaterial.name}");
					AssetDatabase.CreateAsset(cover, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name} {FileName(insert.CoverMaterial.name)}.mat"));
				}
			} catch {
				// Nothing half-forked is left behind: the insert still wears the source.
				AssetDatabase.DeleteAsset(folder);
				throw;
			}

			Undo.RecordObjects(new Object[] { insert, renderer }, "Fork Insert Bake");
			if (cover != null) {
				insert.SetCoverMaterial(cover);
			}
			// A copy that no longer fits the mesh is still the preview the source
			// was, until the bake that follows replaces it.
			insert.SetBake(data);
			PrefabUtility.RecordPrefabInstancePropertyModifications(insert);
			PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
			AssetDatabase.SaveAssets();

			if (!insert.IsBakeFresh) {
				Bake(insert);
			}
			return data;
		}

		/// <summary>A copy of the texture asset, import settings included, or null without one.</summary>
		private static Texture2D CopyTexture(Texture2D texture, string pathWithoutExtension)
		{
			if (texture == null) {
				return null;
			}
			var sourcePath = AssetDatabase.GetAssetPath(texture);
			var path = pathWithoutExtension + Path.GetExtension(sourcePath);
			if (!AssetDatabase.CopyAsset(sourcePath, path)) {
				throw new InvalidOperationException($"{sourcePath} could not be copied to {path}.");
			}
			return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		}

		#endregion

		#region Ownership

		/// <summary>
		/// Whether the bake is the insert's to write over: it was made for this
		/// insert's id, wherever the two now are. The id is made sure of first,
		/// so a duplicate that never passed through OnEnable cannot pass for its
		/// original. A prefab instance never owns the bake it has from its
		/// prefab, and a bake from before inserts had ids is the insert's as long
		/// as no other insert in the open scenes shows it.
		/// </summary>
		public static bool Owns(InsertComponent insert, InsertBakeData bake)
		{
			if (bake == null) {
				return false;
			}
			if (PrefabUtility.IsPartOfPrefabInstance(insert) && !new SerializedObject(insert).FindProperty("_bake").prefabOverride) {
				return false;
			}
			var id = insert.EnsureId();
			if (!string.IsNullOrEmpty(bake.Owner)) {
				return bake.Owner == id;
			}
			return !Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include)
				.Any(other => other != insert && other.Bake == bake);
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
