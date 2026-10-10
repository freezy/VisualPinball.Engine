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
using UnityEditor.SceneManagement;
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
		public const string HybridShaderName = "Pinball/Insert Hybrid";

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
			public int OpticalSize;
			public int ResponseSize;
			public bool HybridOptics;
			public bool UseHybridMaterial;
			/// <summary>The prefab whose package folder the bake goes into, or null for the project.</summary>
			public string PackagePrefab;
			public bool PlainStipple;
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

		/// <param name="insert">The insert to bake</param>
		/// <param name="useHybridMaterial">Whether to put a hybrid material on the insert afterwards</param>
		/// <param name="toPackage">Whether to bake into the package folder of the prefab the insert comes from, when that package can be written to</param>
		/// <param name="prefab">The project prefab the insert is an instance of, to bake into that prefab, which all its instances wear, rather than into the insert</param>
		public static void Bake(InsertComponent insert, bool useHybridMaterial = false, bool toPackage = false, string prefab = null)
		{
			if (_job != null) {
				return;
			}
			LastError = null;
			LastErrorInsert = null;
			string packagePrefab = null;
			if (prefab != null) {
				packagePrefab = prefab;
			} else if (toPackage) {
				packagePrefab = PackagePrefabPath(insert);
				if (packagePrefab == null) {
					Debug.LogWarning($"{insert.name} isn't from a prefab in a package that can be written to, so it's baked into the project as usual.");
				}
			} else {
				// An unchanged instance of a project prefab, like a variant made by
				// Edit in project, bakes the prefab's bake, which all its instances wear.
				packagePrefab = PrefabBakePath(insert);
			}
			var blender = BlenderPath;
			var info = Probe();
			if (info?.Error != null) {
				Fail(insert, info.Error);
				return;
			}

			var mesh = new Mesh();
			Mesh plainMesh = null;
			try {
				var layout = new InsertMeshLayout();
				var body = insert.Generate(mesh, layout);
				var relief = math.max(math.abs(body.ReliefOffset), math.abs(body.StippleHeight));
				InsertBakeRequest request;
				if (body.StippleHeight != 0f) {
					plainMesh = new Mesh();
					var plainLayout = new InsertMeshLayout();
					insert.Generate(plainMesh, plainLayout, true);
					request = InsertBakeRequest.Build(plainMesh, plainLayout, insert.BakeSettings, relief);
					var source = InsertBakeRequest.Build(mesh, layout, insert.BakeSettings, relief);
					request.SetHighSource(source);
				} else {
					request = InsertBakeRequest.Build(mesh, layout, insert.BakeSettings, relief);
				}
				var renderer = insert.GetComponent<MeshRenderer>();
				var hybridShader = Shader.Find(HybridShaderName);
				// A new lit insert has no material worth keeping. Give it a working
				// hybrid body as part of its first bake.
				var setUpNewLight = insert.GetComponent<InsertLightComponent>() != null &&
					renderer.sharedMaterial == null && hybridShader != null;
				useHybridMaterial |= setUpNewLight;
				// A hybrid material always needs maps from this exact UV layout.
				request.HybridOptics |= useHybridMaterial || insert.Bake != null && insert.Bake.Material != null && insert.Bake.Material.HasProperty("_OpticalNormalMap");
				if (useHybridMaterial && hybridShader == null) {
					throw new InvalidOperationException("The HDRP hybrid insert shader is not installed.");
				}

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
					OpticalSize = request.OpticalSize,
					ResponseSize = request.ResponseSize,
					HybridOptics = request.HybridOptics,
					UseHybridMaterial = useHybridMaterial,
					PackagePrefab = packagePrefab,
					PlainStipple = body.StippleHeight != 0f,
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
				if (plainMesh != null) { Object.DestroyImmediate(plainMesh); }
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

			InsertBakeData own;
			string folder, name, owner;
			var packagePrefab = job.PackagePrefab != null && AssetDatabase.LoadMainAssetAtPath(job.PackagePrefab) != null && !IsReadOnly(job.PackagePrefab)
				? job.PackagePrefab
				: null;
			if (packagePrefab != null) {
				// Asked for the package, or an unchanged instance of a project prefab:
				// next to the prefab and named after it. The
				// bake there is the prefab's, so it's updated whichever of the prefab's
				// inserts bakes it, and the prefab's own insert owns it.
				folder = Path.GetDirectoryName(packagePrefab)?.Replace('\\', '/');
				name = FileName(Path.GetFileNameWithoutExtension(packagePrefab));
				own = PackageBake(folder, name);
				owner = PackageOwner(insert);
			} else {
				// A duplicated insert shares its original's bake until it bakes its own,
				// which must not overwrite the original's. A bake that cannot be written
				// to, in a package, is never anyone's to overwrite.
				own = Owns(insert, insert.Bake) && !IsReadOnly(insert.Bake) ? insert.Bake : null;
				folder = own != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(own))?.Replace('\\', '/') : InsertFolder(insert);
				// Other inserts of the same prefab bake into the same folder.
				name = own != null ? FileName(insert.name) : UniqueName(folder, FileName(insert.name));
				owner = insert.EnsureId();
			}
			// Whether the material the insert wears is this bake's own is judged
			// against the maps the bake had, before the new ones replace them.
			var renderer = insert.GetComponent<MeshRenderer>();
			var wornIsOwn = own != null && Carries(renderer.sharedMaterial, own);

			// Not "... Normal.png": the asset library re-applies its own normal map
			// preset to every such file on import, wrapping included.
			var texturePath = own != null && own.NormalMap != null && IsPng(own.NormalMap) ? AssetDatabase.GetAssetPath(own.NormalMap) : $"{folder}/{name}, Normal Map.png";
			File.Copy(Path.Combine(job.Folder, "normal.png"), DiskPath(texturePath), true);
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
			data.PlainStipple = job.PlainStipple;
			// Made sure of even when there was no bake to own: an id never seen
			// by OnEnable could still be a duplicate's or a prefab's.
			data.Owner = owner;
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
			data.OpticalNormalMap = ImportOptics(job, folder, name, "optical-normal.png", "Optical Normals", data.OpticalNormalMap, job.OpticalSize, false);
			// The response is written with the sRGB curve, which the importer takes off again.
			data.OpticalResponseMap = ImportOptics(job, folder, name, "optical-response.png", "Optical Response", data.OpticalResponseMap, job.ResponseSize, true);
			if (result.OpticalLamp?.Length == 4) {
				data.OpticalLamp = new Vector4(result.OpticalLamp[0], result.OpticalLamp[1], result.OpticalLamp[2], result.OpticalLamp[3]);
			}
			data.Packer = result.Report.Packer;
			data.UvFill = result.Report.UvFill;
			// The hybrid step creates the body material. Keep the previous material
			// reference for Undo, without writing a copy that would be replaced.
			if (!job.UseHybridMaterial) {
				data.Material = BakeMaterial(insert, wornIsOwn, folder, name, texture, packagePrefab);
			}
			BindOptics(data);
			if (created) {
				// A package folder may already hold something by that name.
				var bakePath = $"{folder}/{name}, Bake.asset";
				AssetDatabase.CreateAsset(data, AssetDatabase.LoadMainAssetAtPath(bakePath) != null ? AssetDatabase.GenerateUniqueAssetPath(bakePath) : bakePath);
			} else {
				EditorUtility.SetDirty(data);
			}
			AssetDatabase.SaveAssets();

			// A project prefab's bake is the prefab's: what the bake puts on this
			// instance goes into the prefab, for all its instances, and so does the
			// material when the prefab wore its bake's, rather than a colour of its own.
			var projectPrefab = packagePrefab != null && packagePrefab.StartsWith("Assets/", StringComparison.Ordinal) && PrefabUtility.IsPartOfPrefabInstance(insert)
				? PrefabUtility.GetCorrespondingObjectFromSource(insert)
				: null;
			var prefabWoreBake = projectPrefab != null && projectPrefab.TryGetComponent<MeshRenderer>(out var prefabRenderer)
				&& (prefabRenderer.sharedMaterial == null || Carries(prefabRenderer.sharedMaterial, projectPrefab.Bake));

			var wornBeforeBake = renderer.sharedMaterial;
			Undo.RecordObjects(new Object[] { insert, renderer }, "Bake Insert");
			insert.SetBake(data);
			if (job.UseHybridMaterial) {
				// SetBake can put the previous bake's material back. Preserve the
				// material actually worn until the hybrid replaces it, or a fallback
				// needs to derive a plain material from it.
				renderer.sharedMaterial = wornBeforeBake;
			}
			PrefabUtility.RecordPrefabInstancePropertyModifications(insert);
			PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
			if (job.UseHybridMaterial) {
				try {
					UseHybridMaterial(insert, packagePrefab != null);
				} catch (Exception exception) {
					string fallbackError = null;
					try {
						data.Material = BakeMaterial(insert, wornIsOwn, folder, name, texture, packagePrefab);
						EditorUtility.SetDirty(data);
						insert.SetBake(data);
						PrefabUtility.RecordPrefabInstancePropertyModifications(insert);
						PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
						AssetDatabase.SaveAssets();
					} catch (Exception materialException) {
						fallbackError = $" Plain material recovery also failed: {materialException.Message}";
					}
					LastError = $"Bake succeeded, but hybrid material setup failed: {exception.Message}";
					LastError += fallbackError;
					LastErrorInsert = insert;
					Debug.LogError($"{insert.name}: {LastError}");
				}
			}
			if (projectPrefab != null) {
				var serialized = new SerializedObject(insert);
				foreach (var property in new[] { "_bake", "_unbakedMaterial" }.Select(serialized.FindProperty)) {
					if (property.prefabOverride) {
						PrefabUtility.ApplyPropertyOverride(property, packagePrefab, InteractionMode.AutomatedAction);
					}
				}
				var material = new SerializedObject(renderer).FindProperty("m_Materials.Array.data[0]");
				if (prefabWoreBake && material != null && material.prefabOverride) {
					PrefabUtility.ApplyPropertyOverride(material, packagePrefab, InteractionMode.AutomatedAction);
				}
			}
			// The other inserts wearing the bake, like the prefab's other instances, take its new UVs.
			foreach (var other in Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
				if (other != insert && other.Bake == data) {
					other.RebuildMesh();
				}
			}
		}

		private static Texture2D ImportOptics(Job job, string folder, string name, string source, string suffix, Texture2D existing, int size, bool srgb)
		{
			var sourcePath = Path.Combine(job.Folder, source);
			if (!File.Exists(sourcePath)) {
				return null;
			}
			var path = existing != null && IsPng(existing) ? AssetDatabase.GetAssetPath(existing) : $"{folder}/{name}, {suffix}.png";
			File.Copy(sourcePath, DiskPath(path), true);
			AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
			var importer = (TextureImporter)AssetImporter.GetAtPath(path);
			importer.textureType = TextureImporterType.Default;
			importer.sRGBTexture = srgb;
			importer.isReadable = false;
			importer.wrapMode = TextureWrapMode.Clamp;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.maxTextureSize = math.max(32, size);
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
		/// <param name="insert">The insert to put the hybrid material on</param>
		/// <param name="packageBake">Whether the bake was just made into the package of the insert's prefab, which makes it the insert's to use whoever owns it</param>
		public static void UseHybridMaterial(InsertComponent insert, bool packageBake = false)
		{
			var shader = Shader.Find(HybridShaderName);
			var data = insert.Bake;
			if (shader == null || data == null || !insert.IsBakeFresh || data.OpticalNormalMap == null || data.OpticalResponseMap == null) {
				throw new InvalidOperationException("Bake Hybrid Optics first, with the HDRP package installed.");
			}
			if (IsReadOnly(data)) {
				throw new InvalidOperationException($"This bake belongs to {ReadOnlyReason(data)} and cannot be written to. Fork it to the project first.");
			}
			if (!packageBake && !Owns(insert, data)) {
				throw new InvalidOperationException("This bake is not this insert's own. Bake this insert again before choosing its own material.");
			}
			var renderer = insert.GetComponent<MeshRenderer>();
			Undo.RecordObjects(new Object[] { data, renderer }, "Use Hybrid Insert Material");
			// A bake from before ids is claimed on the way. A package's bake stays its prefab's.
			if (!packageBake) {
				data.Owner = insert.Id;
			}
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
		/// written into and the insert still keeps its look. When baking into a
		/// package (<paramref name="packagePrefab"/>), the other inserts of that
		/// prefab don't count: they are to follow the package.
		/// </summary>
		private static Material BakeMaterial(InsertComponent insert, bool wornIsOwn, string folder, string name, Texture2D normalMap, string packagePrefab)
		{
			var worn = insert.GetComponent<MeshRenderer>().sharedMaterial;
			var material = worn;
			// A project prefab's own body is its bake's, also before its first bake,
			// when it's still a copy carrying another prefab's maps.
			var prefabBody = packagePrefab != null && packagePrefab.StartsWith("Assets/", StringComparison.Ordinal) && worn != null
				&& PrefabUtility.GetCorrespondingObjectFromSource(insert.GetComponent<MeshRenderer>()) is { } prefabRenderer && prefabRenderer.sharedMaterial == worn;
			var created = worn == null || !(wornIsOwn || prefabBody) || !EditorUtility.IsPersistent(worn) || IsReadOnly(AssetDatabase.GetAssetPath(worn));
			if (!created && WornElsewhere(worn, insert, packagePrefab)) {
				created = true;
				Debug.Log($"{insert.name}: another insert wears {worn.name} too, so this bake goes into a copy of it.");
			}
			if (created) {
				material = worn != null ? Derive(worn) : new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
				material.name = worn != null ? $"{name} {BaseName(worn)}" : $"{name} Baked";
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
		/// Inserts of <paramref name="ignoredPrefab"/>, when given, are left out.
		/// </summary>
		private static bool WornElsewhere(Material material, InsertComponent insert, string ignoredPrefab = null)
		{
			foreach (var other in Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
				if (other != insert && other.TryGetComponent<MeshRenderer>(out var renderer) && renderer.sharedMaterial == material
					&& (ignoredPrefab == null || PrefabPath(other) != ignoredPrefab)) {
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

		/// <summary>
		/// What a material derived from <paramref name="worn"/> is named after:
		/// the top of its variant chain, less the name of whichever insert it
		/// was made for. A bake of a bake, or a fork of a fork, would otherwise
		/// carry every insert's name along the way.
		/// </summary>
		private static string BaseName(Material worn)
		{
			while (worn.parent != null) {
				worn = worn.parent;
			}
			var baseName = worn.name;
			var inserts = Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include);
			for (var stripped = true; stripped;) {
				stripped = false;
				foreach (var insert in inserts) {
					var prefix = insert.name + " ";
					if (baseName.Length > prefix.Length && baseName.StartsWith(prefix, StringComparison.Ordinal)) {
						baseName = baseName.Substring(prefix.Length);
						stripped = true;
					}
				}
			}
			return baseName;
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

		#endregion

		#region Forking

		/// <summary>
		/// Whether the bake's assets cannot be written to: they live in a package
		/// Unity keeps immutable, in a locked asset library, or version control
		/// holds them.
		/// </summary>
		public static bool IsReadOnly(InsertBakeData bake) => bake != null && IsReadOnly(AssetDatabase.GetAssetPath(bake));

		/// <summary>The physical destination for a project or package asset; imports still use its asset path.</summary>
		private static string DiskPath(string assetPath)
		{
			var package = PackageInfo.FindForAssetPath(assetPath);
			return package != null
				? Path.Combine(package.resolvedPath, assetPath.Substring(package.assetPath.Length).TrimStart('/'))
				: Path.GetFullPath(assetPath);
		}

		public static bool IsReadOnly(string assetPath)
		{
			if (string.IsNullOrEmpty(assetPath)) {
				return false;
			}
			var package = PackageInfo.FindForAssetPath(assetPath);
			if (package != null && package.source != PackageSource.Embedded && package.source != PackageSource.Local) {
				return true;
			}
			if (LockedLibrary(assetPath) != null) {
				return true;
			}
			return !AssetDatabase.IsOpenForEdit(assetPath, StatusQueryOptions.UseCachedIfPossible);
		}

		/// <summary>What keeps the bake read-only, for the inspector to say.</summary>
		public static string ReadOnlyReason(InsertBakeData bake)
		{
			var path = AssetDatabase.GetAssetPath(bake);
			var package = PackageInfo.FindForAssetPath(path);
			if (package != null && package.source != PackageSource.Embedded && package.source != PackageSource.Local) {
				return $"the package {package.displayName}";
			}
			var library = LockedLibrary(path);
			return library != null ? $"the locked asset library {library.Name}" : "version control";
		}

		/// <summary>
		/// The locked asset library the asset belongs to, or null: one whose
		/// folder it is in, or one it is an asset of. A locked library's files
		/// can be written to, but VPE leaves them alone, as it does a package's.
		/// </summary>
		public static AssetLibrary LockedLibrary(string assetPath)
		{
			if (string.IsNullOrEmpty(assetPath)) {
				return null;
			}
			var guid = AssetDatabase.AssetPathToGUID(assetPath);
			foreach (var library in Libraries()) {
				if (library == null || !library.IsLocked) {
					continue;
				}
				var root = library.LibraryRoot?.Replace('\\', '/').TrimEnd('/');
				if (!string.IsNullOrEmpty(root) && assetPath.StartsWith(root + "/", StringComparison.Ordinal)) {
					return library;
				}
				if (!string.IsNullOrEmpty(guid) && library.HasAsset(guid)) {
					return library;
				}
			}
			return null;
		}

		private static AssetLibrary[] _libraries;

		// Inspectors ask on every repaint, so the libraries are looked up once per project change;
		// whether one is locked is read live.
		private static AssetLibrary[] Libraries()
		{
			if (_libraries == null) {
				_libraries = AssetDatabase.FindAssets("t:" + nameof(AssetLibrary))
					.Select(guid => AssetDatabase.LoadAssetAtPath<AssetLibrary>(AssetDatabase.GUIDToAssetPath(guid)))
					.Where(library => library != null)
					.ToArray();
				EditorApplication.projectChanged -= ForgetLibraries;
				EditorApplication.projectChanged += ForgetLibraries;
			}
			return _libraries;
		}

		internal static void ForgetLibraries() => _libraries = null;

		/// <summary>
		/// Gives the insert a bake in the project: copies of the maps and bake
		/// data it wears, bound into the material it wears when that is the
		/// project's own, or into a variant of it when that is a package's, so
		/// an insert from a package can be tuned and baked here while the
		/// package stays as it is. A cover material from a package gets a
		/// variant too. A prefab's instance gets them in a variant of its
		/// prefab, which owns them, as every shape is a prefab of its own and
		/// the insert's duplicates follow it; any other insert owns them itself.
		/// When the copy no longer fits the mesh, a bake follows at once.
		/// </summary>
		public static InsertBakeData Fork(InsertComponent insert)
		{
			if (insert.Bake == null) {
				throw new InvalidOperationException("The insert has no bake to fork.");
			}
			if (IsInstanceRoot(insert)) {
				ForkIntoVariant(insert, Array.Empty<InsertComponent>());
				return insert.Bake;
			}
			var renderer = insert.GetComponent<MeshRenderer>();
			var folder = InsertFolder(insert);
			// Other inserts of the same prefab fork into the same folder.
			var name = UniqueName(folder, FileName(insert.name));
			var made = new List<Object>();

			InsertBakeData data;
			Material cover;
			try {
				(data, cover) = ForkAssets(insert, folder, name, insert.EnsureId(), made, new[] { insert });
			} catch {
				Discard(made, folder);
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

		/// <summary>
		/// The other inserts in the open scenes that a fork of the insert's bake
		/// could go to as well: instances of the same prefab that wear the same
		/// bake, still fitting it, and the same materials. None when the insert
		/// isn't a prefab's instance of its own.
		/// </summary>
		public static InsertComponent[] Sharing(InsertComponent insert)
		{
			if (insert.Bake == null || !IsInstanceRoot(insert)) {
				return Array.Empty<InsertComponent>();
			}
			var prefab = PrefabPath(insert);
			var body = insert.GetComponent<MeshRenderer>().sharedMaterial;
			return Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None)
				.Where(other => other != insert && IsInstanceRoot(other) && PrefabPath(other) == prefab
					&& other.Bake == insert.Bake && other.IsBakeFresh
					&& other.GetComponent<MeshRenderer>().sharedMaterial == body && other.CoverMaterial == insert.CoverMaterial)
				.OrderBy(other => other.name)
				.ToArray();
		}

		/// <summary>Whether the insert is a prefab's instance of its own, rather than part of another's or of none.</summary>
		public static bool IsInstanceRoot(InsertComponent insert)
			=> PrefabUtility.IsPartOfPrefabInstance(insert) && PrefabUtility.GetOutermostPrefabInstanceRoot(insert.gameObject) == insert.gameObject;

		/// <summary>
		/// Edits the insert in the project, with the others that share its
		/// prefab, shape and materials: a prefab's instance becomes an instance
		/// of a variant of it, any other insert gets copies of what it has that
		/// can't be changed. Either can then be recoloured, tuned and reshaped.
		/// </summary>
		public static void EditInProject(InsertComponent insert, IReadOnlyCollection<InsertComponent> others)
		{
			if (IsInstanceRoot(insert)) {
				ForkIntoVariant(insert, others);
				return;
			}
			// An insert of no prefab owns its copies itself: of a bake that can't
			// be baked over, with the materials bound to it, and of what's left.
			if (insert.Bake != null && IsReadOnly(insert.Bake)) {
				Fork(insert);
			}
			InsertColorPresets.CopyToProject(new[] { insert }.Concat(others).Distinct().ToArray());
		}

		/// <summary>
		/// Edits the inserts in the project that need it, each with those of the
		/// others that share its prefab, shape and materials.
		/// </summary>
		public static void EditInProject(IReadOnlyCollection<InsertComponent> inserts)
		{
			var left = inserts.Where(NeedsEditing).ToList();
			while (left.Count > 0) {
				var insert = left[0];
				var group = Sharing(insert).Where(left.Contains).ToArray();
				EditInProject(insert, group);
				left.Remove(insert);
				left.RemoveAll(group.Contains);
			}
		}

		/// <summary>Whether the insert can be baked where it is: its bake can be written to, or its prefab, in the project, takes the bake.</summary>
		public static bool BakesHere(InsertComponent insert)
			=> insert.Bake == null || !IsReadOnly(insert.Bake) || ProjectPrefab(insert) != null;

		/// <summary>Whether something the insert has can't be changed, so it's to be edited in the project first.</summary>
		public static bool NeedsEditing(InsertComponent insert)
			=> !BakesHere(insert) || InsertColorPresets.HasReadOnlyMaterial(insert);

		/// <summary>The prefab in the project, which can be written to, that the insert is an instance of, or null.</summary>
		private static string ProjectPrefab(InsertComponent insert)
		{
			if (!IsInstanceRoot(insert)) {
				return null;
			}
			var path = PrefabPath(insert);
			return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal) && !IsReadOnly(path) ? path : null;
		}

		/// <summary>
		/// Makes a variant of the insert's prefab in the project, and the insert
		/// and the given others instances of it: the variant holds the insert's
		/// shape and materials, so they follow it from then on. Their own
		/// positions and other overrides are kept. A material that can't be
		/// changed, or that inserts not following the variant wear, is copied,
		/// as a material variant that keeps following it; a cover the project
		/// can change is shared, as it is a colour's rather than a shape's. The
		/// bake stays the one the insert wears until the variant is baked, which
		/// makes it one of its own, next to the variant and named after it.
		/// This is a library insert's edit, and an insert of a project prefab
		/// reshaped on its own becoming a prefab of its own shape. When the
		/// bake no longer fits the shape, a bake follows at once.
		/// </summary>
		/// <returns>The variant</returns>
		public static GameObject ForkIntoVariant(InsertComponent insert, IReadOnlyCollection<InsertComponent> others)
		{
			if (!IsInstanceRoot(insert)) {
				throw new InvalidOperationException("The insert is no prefab instance of its own.");
			}
			var prefabPath = PrefabPath(insert);
			var folder = InsertFolder(insert);
			// The variant's bake will be named after it, which is where a bake of its
			// inserts looks for it, so neither may be taken: an insert's own fork may
			// already go by the prefab's name.
			var prefabName = FileName(Path.GetFileNameWithoutExtension(prefabPath));
			var name = prefabName;
			for (var i = 2; UniqueName(folder, name) != name || AssetDatabase.LoadMainAssetAtPath($"{folder}/{name}.prefab") != null; i++) {
				name = $"{prefabName} {i}";
			}
			var variantPath = $"{folder}/{name}.prefab";
			var id = Guid.NewGuid().ToString("N");
			var inserts = new[] { insert }.Concat(others).Distinct().ToArray();
			var shape = ShapeOverrides(insert);
			var made = new List<Object>();

			GameObject variant;
			GameObject temp = null;
			try {
				var renderer = insert.GetComponent<MeshRenderer>();
				var body = renderer.sharedMaterial;
				// The variant's bake will go into its body, which no insert outside it may wear.
				if (body != null && (InsertColorPresets.IsReadOnly(insert, body) || WornOutside(body, inserts))) {
					body = Copy(body, folder, name, made);
				}
				var cover = insert.CoverMaterial;
				if (cover != null && InsertColorPresets.IsReadOnly(insert, cover)) {
					cover = Copy(cover, folder, name, made);
				}
				// A fresh instance of the prefab takes the shape only, not what
				// else this insert sets, like its lamp, which the others don't share.
				temp = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
				var tempInsert = temp.GetComponent<InsertComponent>();
				var from = new SerializedObject(insert);
				var to = new SerializedObject(tempInsert);
				foreach (var path in shape) {
					to.CopyFromSerializedProperty(from.FindProperty(path));
				}
				to.FindProperty("_id").stringValue = id;
				to.ApplyModifiedPropertiesWithoutUndo();
				if (insert.Bake != tempInsert.Bake) {
					tempInsert.SetBake(insert.Bake);
				}
				if (cover != tempInsert.CoverMaterial) {
					tempInsert.SetCoverMaterial(cover);
				}
				var tempRenderer = temp.GetComponent<MeshRenderer>();
				var materials = tempRenderer.sharedMaterials;
				materials[0] = body;
				tempRenderer.sharedMaterials = materials;
				PrefabUtility.RecordPrefabInstancePropertyModifications(tempInsert);
				PrefabUtility.RecordPrefabInstancePropertyModifications(temp.GetComponent<MeshRenderer>());
				variant = PrefabUtility.SaveAsPrefabAsset(temp, variantPath, out var saved);
				if (!saved || variant == null) {
					throw new InvalidOperationException($"The prefab variant could not be saved at {variantPath}.");
				}
				made.Add(variant);
			} catch {
				Discard(made, folder);
				throw;
			} finally {
				if (temp != null) {
					Object.DestroyImmediate(temp);
				}
			}

			Undo.IncrementCurrentGroup();
			var group = Undo.GetCurrentGroup();
			var settings = new PrefabReplacingSettings {
				logInfo = false,
				changeRootNameToAssetName = false,
				objectMatchMode = ObjectMatchMode.ByHierarchy,
				prefabOverridesOptions = PrefabOverridesOptions.KeepAllPossibleOverrides,
			};
			var follows = shape.Concat(new[] { "_bake", "_unbakedMaterial", "_coverMaterial" }).ToArray();
			foreach (var go in inserts.Select(each => each.gameObject).ToArray()) {
				PrefabUtility.ReplacePrefabAssetOfPrefabInstance(go, variant, settings, InteractionMode.UserAction);
				// What the variant now holds is followed rather than overridden.
				var component = go.GetComponent<InsertComponent>();
				var serialized = new SerializedObject(component);
				foreach (var path in follows) {
					var property = serialized.FindProperty(path);
					if (property != null && property.prefabOverride) {
						PrefabUtility.RevertPropertyOverride(property, InteractionMode.UserAction);
					}
				}
				var materials = new SerializedObject(go.GetComponent<MeshRenderer>()).FindProperty("m_Materials");
				if (materials.prefabOverride) {
					PrefabUtility.RevertPropertyOverride(materials, InteractionMode.UserAction);
				}
				component.RebuildMesh();
			}
			Undo.SetCurrentGroupName("Edit Inserts in Project");
			Undo.CollapseUndoOperations(group);
			AssetDatabase.SaveAssets();

			if (!insert.IsBakeFresh) {
				Bake(insert);
			}
			return variant;
		}

		/// <summary>
		/// Whether anything but the inserts wears the material as its body: an
		/// insert in the open scenes, or the prefab of the first of them, which
		/// keeps wearing it for its instances elsewhere.
		/// </summary>
		private static bool WornOutside(Material material, IReadOnlyCollection<InsertComponent> inserts)
		{
			var source = PrefabUtility.GetCorrespondingObjectFromSource(inserts.First().GetComponent<MeshRenderer>());
			if (source != null && source.sharedMaterial == material) {
				return true;
			}
			return Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None)
				.Any(other => !inserts.Contains(other) && other.GetComponent<MeshRenderer>().sharedMaterial == material);
		}

		/// <summary>A variant of the material in the folder, named after the one it's for, which keeps following the material until a value is set.</summary>
		private static Material Copy(Material material, string folder, string name, List<Object> made)
		{
			var copy = Variant(material, $"{name} {BaseName(material)}");
			// Unique, because the body and the cover may share a name.
			AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name} {FileName(BaseName(material))}.mat"));
			made.Add(copy);
			return copy;
		}

		/// <summary>
		/// The insert's properties that its instance overrides and that make its
		/// shape and bake, by their top-level names: not its id, its bake and
		/// materials, nor the cover's playfield, which are its own.
		/// </summary>
		private static string[] ShapeOverrides(InsertComponent insert)
		{
			var source = PrefabUtility.GetCorrespondingObjectFromSource(insert);
			return (PrefabUtility.GetPropertyModifications(insert) ?? Array.Empty<PropertyModification>())
				.Where(modification => modification.target == source)
				.Select(modification => modification.propertyPath.Split('.')[0])
				.Where(path => !path.StartsWith("m_", StringComparison.Ordinal) && !NotShape.Contains(path))
				.Distinct()
				.ToArray();
		}

		private static readonly HashSet<string> NotShape = new() {
			"_id", "_bake", "_unbakedMaterial", "_coverMaterial", "_usePlayfieldNormals", "_coverPlayfield", "_usesPackagedMesh", "_generatedMesh",
		};

		/// <summary>
		/// The prefab in the project whose bake the insert is to bake: the one it
		/// is an instance of, when it wears the prefab's bake and doesn't change
		/// its shape, so the bake fits every instance alike. Null otherwise. Only
		/// the bake named after the prefab, next to it, is baked over; any other
		/// gives way to a new one there, which the prefab then wears.
		/// </summary>
		private static string PrefabBakePath(InsertComponent insert)
		{
			if (insert.Bake == null || !PrefabUtility.IsPartOfPrefabInstance(insert)) {
				return null;
			}
			var path = PrefabPath(insert);
			if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || IsReadOnly(path)) {
				return null;
			}
			var source = PrefabUtility.GetCorrespondingObjectFromSource(insert);
			if (source == null || source.Bake != insert.Bake) {
				return null;
			}
			return ShapeOverrides(insert).Length == 0 ? path : null;
		}

		/// <summary>
		/// The project prefab the insert is an instance of, when the instance
		/// changes the prefab's shape: the shape then either goes into the
		/// prefab, for all its instances, or makes a prefab of its own. Null
		/// otherwise, and for a prefab that can't be written to.
		/// </summary>
		public static string ReshapedPrefab(InsertComponent insert)
		{
			var path = insert.Bake != null ? ProjectPrefab(insert) : null;
			return path != null && ShapeOverrides(insert).Length > 0 ? path : null;
		}

		/// <summary>
		/// The other instances in the open scenes of the prefab the insert is an
		/// instance of that keep its shape, and so would follow a shape put into it.
		/// </summary>
		public static InsertComponent[] Followers(InsertComponent insert)
		{
			if (!IsInstanceRoot(insert)) {
				return Array.Empty<InsertComponent>();
			}
			var prefab = PrefabPath(insert);
			return Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None)
				.Where(other => other != insert && IsInstanceRoot(other) && PrefabPath(other) == prefab && ShapeOverrides(other).Length == 0)
				.ToArray();
		}

		/// <summary>
		/// Puts the shape the insert's instance changes into its prefab, which
		/// all its instances that keep the prefab's shape then have too. A bake
		/// is to follow, of the prefab.
		/// </summary>
		public static void ApplyShape(InsertComponent insert)
		{
			var path = ReshapedPrefab(insert);
			if (path == null) {
				throw new InvalidOperationException("The insert doesn't change the shape of a prefab in the project.");
			}
			var followers = Followers(insert);
			Undo.IncrementCurrentGroup();
			var group = Undo.GetCurrentGroup();
			var serialized = new SerializedObject(insert);
			foreach (var name in ShapeOverrides(insert)) {
				PrefabUtility.ApplyPropertyOverride(serialized.FindProperty(name), path, InteractionMode.UserAction);
			}
			Undo.SetCurrentGroupName("Apply Insert Shape to Prefab");
			Undo.CollapseUndoOperations(group);
			insert.RebuildMesh();
			foreach (var other in followers) {
				other.RebuildMesh();
			}
		}

		/// <summary>
		/// Copies the bake the insert wears, and its maps, into the folder under
		/// the name, owned by the owner, with the material the insert wears
		/// taking the copies: a colour of the user's own carrying the source's
		/// maps in place, when it is the project's and none but the wearers
		/// wear it; the source's own material, or a package's, as a variant that
		/// keeps following its tuning, since the source is left as it was. A
		/// cover material from a package gets a variant too.
		/// </summary>
		private static (InsertBakeData data, Material cover) ForkAssets(InsertComponent insert, string folder, string name, string owner, List<Object> made, IReadOnlyCollection<InsertComponent> wearers)
		{
			var source = insert.Bake;
			var data = Object.Instantiate(source);
			data.name = $"{name}, Bake";
			data.Owner = owner;
			data.NormalMap = CopyTexture(source.NormalMap, $"{folder}/{name}, Normal Map");
			made.Add(data.NormalMap);
			data.OpticalNormalMap = CopyTexture(source.OpticalNormalMap, $"{folder}/{name}, Optical Normals");
			made.Add(data.OpticalNormalMap);
			data.OpticalResponseMap = CopyTexture(source.OpticalResponseMap, $"{folder}/{name}, Optical Response");
			made.Add(data.OpticalResponseMap);
			AssetDatabase.CreateAsset(data, $"{folder}/{name}, Bake.asset");
			made.Add(data);
			var renderer = insert.GetComponent<MeshRenderer>();
			var worn = renderer.sharedMaterial != null ? renderer.sharedMaterial : source.Material;
			if (worn != null) {
				var own = worn != source.Material && Carries(worn, source) && EditorUtility.IsPersistent(worn)
					&& !IsReadOnly(AssetDatabase.GetAssetPath(worn))
					&& !Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None)
						.Any(other => !wearers.Contains(other) && other.GetComponent<MeshRenderer>().sharedMaterial == worn);
				var material = own ? worn : Variant(worn, $"{name} {BaseName(worn)}");
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
					AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name} {FileName(BaseName(worn))}.mat"));
					made.Add(material);
				}
				data.Material = material;
				BindOptics(data);
				EditorUtility.SetDirty(data);
			}
			Material cover = null;
			if (insert.CoverMaterial != null && IsReadOnly(AssetDatabase.GetAssetPath(insert.CoverMaterial))) {
				cover = Variant(insert.CoverMaterial, $"{name} {BaseName(insert.CoverMaterial)}");
				AssetDatabase.CreateAsset(cover, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name} {FileName(BaseName(insert.CoverMaterial))}.mat"));
				made.Add(cover);
			}
			return (data, cover);
		}

		/// <summary>
		/// Deletes what a fork that failed made, so nothing half-forked is left
		/// behind and the inserts still wear the source. The folder may be
		/// shared, so only what the fork made goes, and the folder only when
		/// that leaves it empty.
		/// </summary>
		private static void Discard(IEnumerable<Object> made, string folder)
		{
			foreach (var asset in made) {
				var path = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
				if (!string.IsNullOrEmpty(path)) {
					AssetDatabase.DeleteAsset(path);
				}
			}
			if (folder.StartsWith(OutputRoot + "/", StringComparison.Ordinal) && !AssetDatabase.FindAssets(string.Empty, new[] { folder }).Any()) {
				AssetDatabase.DeleteAsset(folder);
			}
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

		/// <summary>
		/// Where the insert's own assets go in the project when it has none yet:
		/// its bake, its maps, and the materials made for it. For an insert of a
		/// prefab, that's the prefab's folder mirrored under Assets, which every
		/// insert of that prefab shares; for any other, a folder of its own.
		/// </summary>
		public static string InsertFolder(InsertComponent insert)
		{
			var prefab = PrefabPath(insert);
			var folder = string.IsNullOrEmpty(prefab) ? null : MirroredFolder(Path.GetDirectoryName(prefab)?.Replace('\\', '/'));
			// A locked library in the project mirrors onto itself: what's written there would be the library's again.
			if (folder == null || LockedLibrary(folder + "/") != null) {
				return NewFolder(insert);
			}
			EnsureFolder(folder);
			return folder;
		}

		/// <summary>
		/// Where materials made for the insert go: next to the bake it owns, when
		/// that can be written to, or else in its folder.
		/// </summary>
		public static string MaterialFolder(InsertComponent insert)
		{
			var own = Owns(insert, insert.Bake) && !IsReadOnly(insert.Bake) ? insert.Bake : null;
			return own != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(own))?.Replace('\\', '/') : InsertFolder(insert);
		}

		/// <summary>
		/// A package's or the project's folder at the same place under Assets,
		/// less the asset library's Library folder:
		/// <c>Packages/org.visualpinball.unity.assets/Assets/Library/Inserts/X</c>
		/// is <c>Assets/Inserts/X</c>. Null for a folder that is neither.
		/// </summary>
		public static string MirroredFolder(string folder)
		{
			if (string.IsNullOrEmpty(folder)) {
				return null;
			}
			string relative;
			if (folder.StartsWith("Packages/", StringComparison.Ordinal)) {
				var slash = folder.IndexOf('/', "Packages/".Length);
				relative = slash < 0 ? string.Empty : folder.Substring(slash + 1);
				relative = relative == "Assets" ? string.Empty : relative.StartsWith("Assets/", StringComparison.Ordinal) ? relative.Substring("Assets/".Length) : relative;
			} else if (folder == "Assets" || folder.StartsWith("Assets/", StringComparison.Ordinal)) {
				relative = folder.Length > "Assets/".Length ? folder.Substring("Assets/".Length) : string.Empty;
			} else {
				return null;
			}
			relative = relative == "Library" ? string.Empty : relative.StartsWith("Library/", StringComparison.Ordinal) ? relative.Substring("Library/".Length) : relative;
			return relative.Length == 0 ? "Assets" : "Assets/" + relative;
		}

		/// <summary>
		/// A name for an insert's new files in a folder other inserts may share:
		/// its own, or with a number when another's files already go by it.
		/// </summary>
		public static string UniqueName(string folder, string name)
		{
			var disk = DiskPath(folder);
			for (var i = 1; ; i++) {
				var candidate = i == 1 ? name : $"{name} {i}";
				if (!Directory.Exists(disk) || !Directory.EnumerateFiles(disk, candidate + ", *").Any(file => !file.EndsWith(".meta", StringComparison.Ordinal))) {
					return candidate;
				}
			}
		}

		private static void EnsureFolder(string folder)
		{
			var parts = folder.Split('/');
			var path = parts[0];
			for (var i = 1; i < parts.Length; i++) {
				var child = path + "/" + parts[i];
				if (!AssetDatabase.IsValidFolder(child)) {
					AssetDatabase.CreateFolder(path, parts[i]);
				}
				path = child;
			}
		}

		/// <summary>
		/// The prefab the insert comes from, when it lives in a package that can
		/// be written to, or null. Baking into the package puts the bake next to it.
		/// </summary>
		private static string PackagePrefabPath(InsertComponent insert)
		{
			var path = PrefabPath(insert);
			return !string.IsNullOrEmpty(path) && path.StartsWith("Packages/", StringComparison.Ordinal) && !IsReadOnly(path) ? path : null;
		}

		/// <summary>The prefab asset the insert is an instance of, or the one open in prefab mode, or null.</summary>
		private static string PrefabPath(InsertComponent insert)
		{
			if (PrefabUtility.IsPartOfPrefabInstance(insert)) {
				return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(insert.gameObject);
			}
			var stage = PrefabStageUtility.GetPrefabStage(insert.gameObject);
			return stage != null ? stage.assetPath : null;
		}

		/// <summary>
		/// The bake in the package folder to update: the one named after the
		/// prefab. Not whatever bake the insert wears, even in that folder: a
		/// prefab duplicated next to its original still wears the original's
		/// bake, and its id too, which would then be overwritten.
		/// </summary>
		private static InsertBakeData PackageBake(string folder, string name)
		{
			var bake = AssetDatabase.LoadAssetAtPath<InsertBakeData>($"{folder}/{name}, Bake.asset");
			return bake != null && !IsReadOnly(bake) ? bake : null;
		}

		/// <summary>
		/// Who owns a bake in the package: the prefab's own insert, so baking in
		/// prefab mode keeps updating it, rather than whichever instance baked it.
		/// </summary>
		private static string PackageOwner(InsertComponent insert)
		{
			if (!PrefabUtility.IsPartOfPrefabInstance(insert)) {
				return insert.EnsureId();
			}
			var source = PrefabUtility.GetCorrespondingObjectFromSource(insert);
			return source != null && !string.IsNullOrEmpty(source.Id) ? source.Id : string.Empty;
		}

		internal static string FileName(string name)
		{
			var clean = Regex.Replace(name, @"[<>:""/\\|?*\x00-\x1f]", "_").Trim().TrimEnd('.');
			return string.IsNullOrEmpty(clean) ? "Insert" : clean;
		}

		private static void Fail(InsertComponent insert, string error)
		{
			LastError = $"Bake failed: {error}";
			LastErrorInsert = insert;
			Debug.LogError($"Baking {(insert != null ? insert.name : "an insert")} failed: {error}");
		}

		#endregion
	}
}
