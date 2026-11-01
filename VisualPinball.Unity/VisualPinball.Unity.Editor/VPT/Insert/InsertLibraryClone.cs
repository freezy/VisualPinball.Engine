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
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Clones an insert of an asset library that can be written to into a new
	/// insert of the same library: its folder, next to the source's, holds
	/// copies of the prefab, its bake and maps, and its body materials with
	/// every color, wherever they lived, all named after it and pointing at
	/// each other. The covers stay the library's shared ones. The clone owns
	/// its bake, so reshaping and baking it updates its own assets, and it's
	/// added to the library with the source's metadata and material
	/// variations. The Asset Browser offers it on the inserts of its libraries
	/// that aren't locked.
	/// </summary>
	public static class InsertLibraryClone
	{
		/// <summary>Asks for the clone's name, then clones the insert under it.</summary>
		/// <param name="prefabPath">The insert prefab to clone</param>
		/// <param name="cloned">Called with the new prefab</param>
		public static void ShowCloneDialog(string prefabPath, Action<GameObject> cloned)
			=> InsertCloneNameWindow.Show(prefabPath, name => cloned(Clone(prefabPath, name)));

		/// <summary>Why the asset can't be cloned, or null when it can: an insert prefab of an asset library that can be written to.</summary>
		public static string CannotClone(string prefabPath)
		{
			if (string.IsNullOrEmpty(prefabPath) || !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) {
				return "Not a prefab.";
			}
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
			if (prefab == null || prefab.GetComponent<InsertComponent>() == null) {
				return "Not an insert prefab.";
			}
			var library = LibraryOf(prefabPath);
			if (library == null) {
				return "The insert isn't part of an asset library.";
			}
			if (library.IsLocked || InsertBaker.IsReadOnly(prefabPath)) {
				return $"The asset library {library.Name} can't be written to.";
			}
			return null;
		}

		/// <summary>Why the insert can't be cloned under the name, or null when it can.</summary>
		public static string CannotName(string prefabPath, string name)
		{
			if (string.IsNullOrWhiteSpace(name)) {
				return "Enter a name.";
			}
			if (name == Path.GetFileNameWithoutExtension(prefabPath)) {
				return "Choose a name other than the source's.";
			}
			if (InsertBaker.FileName(name) != name) {
				return "The name can't have any of < > : \" / \\ | ? * nor end with a dot.";
			}
			if (AssetDatabase.IsValidFolder(TargetFolder(prefabPath, name))) {
				return $"There already is a folder called \"{name}\".";
			}
			return null;
		}

		/// <summary>The folder of the clone, next to the source's.</summary>
		public static string TargetFolder(string prefabPath, string name)
			=> $"{Path.GetDirectoryName(Path.GetDirectoryName(prefabPath))?.Replace('\\', '/')}/{name}";

		/// <summary>Clones the insert into a new folder of the same library, under the name.</summary>
		/// <returns>The new prefab</returns>
		public static GameObject Clone(string prefabPath, string name)
		{
			var error = CannotClone(prefabPath) ?? CannotName(prefabPath, name);
			if (error != null) {
				throw new InvalidOperationException(error);
			}
			var library = LibraryOf(prefabPath);
			var source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<InsertComponent>();
			var sourceFolder = Path.GetDirectoryName(prefabPath)!.Replace('\\', '/');
			var folder = TargetFolder(prefabPath, name);
			var parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
			var sourceGuid = AssetDatabase.AssetPathToGUID(prefabPath);
			var sourceAsset = library.HasAsset(sourceGuid) ? library.GetAsset(sourceGuid) : null;

			// The covers are the library's, wherever they live: the clone keeps them.
			var covers = new HashSet<Material>(sourceAsset?.MaterialVariations?
				.Where(variation => variation.Target?.Slot == 1)
				.SelectMany(variation => variation.Overrides ?? new List<AssetMaterialOverride>())
				.Select(o => o.Material)
				.Where(material => material != null) ?? Enumerable.Empty<Material>());
			if (source.CoverMaterial != null) {
				covers.Add(source.CoverMaterial);
			}
			var inside = Directory.GetFiles(InsertBaker.DiskPath(sourceFolder))
				.Where(file => !file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
				.Select(file => $"{sourceFolder}/{Path.GetFileName(file)}")
				.Where(path => !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
				.Where(path => !(AssetDatabase.LoadMainAssetAtPath(path) is Material material && covers.Contains(material)))
				.ToArray();
			var files = inside.Concat(OwnAssetsElsewhere(source, sourceAsset, sourceFolder, covers, inside.Select(AssetDatabase.LoadMainAssetAtPath).OfType<Material>())).ToArray();
			var stems = Stems(Path.GetFileNameWithoutExtension(prefabPath), files.Select(AssetDatabase.LoadMainAssetAtPath).OfType<Material>())
				.Concat(BakeStem(source.Bake))
				.ToArray();

			AssetDatabase.CreateFolder(parent, name);
			Asset asset = null;
			try {
				var guids = new Dictionary<string, string>();
				var copies = new List<string>();
				foreach (var path in files) {
					var copy = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{Rename(Path.GetFileNameWithoutExtension(path), stems, name)}{Path.GetExtension(path)}");
					if (!AssetDatabase.CopyAsset(path, copy)) {
						throw new InvalidOperationException($"{path} could not be copied to {copy}.");
					}
					guids[AssetDatabase.AssetPathToGUID(path)] = AssetDatabase.AssetPathToGUID(copy);
					copies.Add(copy);
				}
				var prefabCopy = $"{folder}/{Rename(Path.GetFileNameWithoutExtension(prefabPath), stems, name)}.prefab";

				// The copies still point at the source's assets and carry its insert's id,
				// which owns its bake: they're pointed at each other, under a new id.
				var oldId = source.Id;
				var newId = Guid.NewGuid().ToString("N");
				foreach (var copy in copies) {
					var disk = InsertBaker.DiskPath(copy);
					var text = File.ReadAllText(disk);
					if (!text.StartsWith("%YAML", StringComparison.Ordinal)) {
						continue;
					}
					var remapped = guids.Aggregate(text, (current, guid) => current.Replace(guid.Key, guid.Value));
					if (!string.IsNullOrEmpty(oldId)) {
						remapped = remapped.Replace($"_id: {oldId}", $"_id: {newId}").Replace($"Owner: {oldId}", $"Owner: {newId}");
					}
					if (remapped != text) {
						File.WriteAllText(disk, remapped);
					}
				}
				foreach (var copy in copies) {
					AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
				}

				// Named after their files, which they were not yet.
				foreach (var copy in copies) {
					var main = AssetDatabase.LoadMainAssetAtPath(copy);
					var fileName = Path.GetFileNameWithoutExtension(copy);
					if ((main is Material || main is ScriptableObject) && main.name != fileName) {
						main.name = fileName;
						EditorUtility.SetDirty(main);
					}
				}
				AssetDatabase.SaveAssets();
				var root = PrefabUtility.LoadPrefabContents(prefabCopy);
				try {
					root.name = Path.GetFileNameWithoutExtension(prefabCopy);
					// Also when it's a variant's override, which the text didn't match.
					var id = new SerializedObject(root.GetComponent<InsertComponent>());
					id.FindProperty("_id").stringValue = newId;
					// What the source wore before its bake is the clone's only when it was
					// copied with it. Another insert's, which a clone of a clone carries,
					// would come back when the clone's bake is removed.
					var preBake = id.FindProperty("_unbakedMaterial");
					if (preBake.objectReferenceValue != null && !AssetDatabase.GetAssetPath(preBake.objectReferenceValue).StartsWith(folder + "/", StringComparison.Ordinal)) {
						preBake.objectReferenceValue = null;
					}
					id.ApplyModifiedPropertiesWithoutUndo();
					PrefabUtility.SaveAsPrefabAsset(root, prefabCopy);
				} finally {
					PrefabUtility.UnloadPrefabContents(root);
				}
				var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabCopy);
				// The bake in the clone's folder is the clone's, also when the source's
				// was made by another insert's id, which then still owned it.
				var bake = prefab.GetComponent<InsertComponent>().Bake;
				if (bake != null && AssetDatabase.GetAssetPath(bake).StartsWith(folder + "/", StringComparison.Ordinal) && bake.Owner != newId) {
					bake.Owner = newId;
					EditorUtility.SetDirty(bake);
					AssetDatabase.SaveAssets();
				}

				if (sourceAsset != null) {
					var category = sourceAsset.Category ?? library.GetCategories().FirstOrDefault(sourceAsset.IsOfCategory);
					if (category != null) {
						library.AddAsset(prefab, category);
						asset = library.GetAsset(AssetDatabase.AssetPathToGUID(prefabCopy));
						asset.CopyMetadataFrom(sourceAsset);
						// The body colors are the clone's own, the covers stay shared.
						foreach (var o in asset.MaterialVariations.Concat(asset.DecalVariations).SelectMany(variation => variation.Overrides)) {
							if (o.Material != null && guids.TryGetValue(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(o.Material)), out var guid)) {
								o.Material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
							}
						}
						asset.Save();
					} else {
						Debug.LogWarning($"{Path.GetFileName(prefabPath)} has no category in {library.Name}, so the clone isn't added to it. Drop it into the Asset Browser to add it.");
					}
				}
				AssetDatabase.SaveAssets();
				Debug.Log($"Cloned {Path.GetFileNameWithoutExtension(prefabPath)} to {prefabCopy}.");
				return prefab;
			} catch {
				if (asset != null) {
					library.DeleteAsset(asset);
				}
				AssetDatabase.DeleteAsset(folder);
				throw;
			}
		}

		/// <summary>
		/// The insert's own assets outside its folder, which a prefab duplicated
		/// into a folder of its own still points at: its bake with the bake's maps
		/// and material, its body with every color, and the bodies those are
		/// variants of that carry the bake. Not the covers, nor what isn't a file
		/// of its own, like a built-in material.
		/// </summary>
		private static IEnumerable<string> OwnAssetsElsewhere(InsertComponent source, Asset sourceAsset, string sourceFolder, ICollection<Material> covers, IEnumerable<Material> insideMaterials)
		{
			var bake = source.Bake;
			var assets = new List<Object>();
			var materials = new List<Material> { source.GetComponent<MeshRenderer>().sharedMaterial };
			materials.AddRange(insideMaterials);
			materials.AddRange(sourceAsset?.MaterialVariations?
				.Where(variation => variation.Target?.Slot == 0)
				.SelectMany(variation => variation.Overrides ?? new List<AssetMaterialOverride>())
				.Select(o => o.Material) ?? Enumerable.Empty<Material>());
			if (bake != null) {
				assets.AddRange(new Object[] { bake, bake.NormalMap, bake.OpticalNormalMap, bake.OpticalResponseMap });
				materials.Add(bake.Material);
			}
			var seen = new HashSet<Material>();
			var pending = new Queue<Material>(materials.Where(material => material != null));
			while (pending.Count > 0) {
				var material = pending.Dequeue();
				if (covers.Contains(material) || !seen.Add(material)) {
					continue;
				}
				assets.Add(material);
				if (material.parent != null && InsertBaker.Carries(material.parent, bake)) {
					pending.Enqueue(material.parent);
				}
			}
			return assets
				.Where(asset => asset != null && AssetDatabase.IsMainAsset(asset))
				.Select(AssetDatabase.GetAssetPath)
				.Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal))
				.Where(path => !path.StartsWith(sourceFolder + "/", StringComparison.Ordinal))
				.Distinct();
		}

		/// <summary>The name the bake's files start with, which is its prefab's, also when that's another than the source.</summary>
		private static IEnumerable<string> BakeStem(InsertBakeData bake)
		{
			var fileName = bake != null ? Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(bake)) : null;
			return fileName != null && fileName.EndsWith(", Bake", StringComparison.Ordinal)
				? new[] { fileName.Substring(0, fileName.Length - ", Bake".Length) }
				: Array.Empty<string>();
		}

		/// <summary>
		/// The beginnings of the source's file names that are its name: the
		/// prefab's, and the body's of which the colors are variants, which
		/// isn't always the prefab's.
		/// </summary>
		public static string[] Stems(string prefabName, IEnumerable<Material> materials)
			=> new[] { prefabName }
				.Concat(materials.Select(material => material.parent).Where(parent => parent != null).Select(parent => parent.name))
				.Distinct()
				.OrderByDescending(stem => stem.Length)
				.ToArray();

		/// <summary>The file name with the longest of the stems it starts with replaced by the name, or the name put in front of it.</summary>
		public static string Rename(string fileName, IEnumerable<string> stems, string name)
		{
			foreach (var stem in stems.OrderByDescending(stem => stem.Length)) {
				if (fileName == stem) {
					return name;
				}
				if (fileName.Length > stem.Length && fileName.StartsWith(stem, StringComparison.Ordinal) && fileName[stem.Length] is ',' or ' ' or '(') {
					return name + fileName.Substring(stem.Length);
				}
			}
			return $"{name}, {fileName}";
		}

		/// <summary>The asset library whose folder the asset is in, or null.</summary>
		private static AssetLibrary LibraryOf(string assetPath)
			=> AssetDatabase.FindAssets("t:" + nameof(AssetLibrary))
				.Select(guid => AssetDatabase.LoadAssetAtPath<AssetLibrary>(AssetDatabase.GUIDToAssetPath(guid)))
				.FirstOrDefault(library => library != null && !string.IsNullOrEmpty(library.LibraryRoot)
					&& assetPath.StartsWith(library.LibraryRoot.Replace('\\', '/').TrimEnd('/') + "/", StringComparison.Ordinal));
	}

	/// <summary>Asks for the name of a cloned insert, the source's to start with.</summary>
	internal class InsertCloneNameWindow : EditorWindow
	{
		private string _prefabPath;
		private string _name;
		private Action<string> _done;
		private bool _focused;
		private string _error;

		public static void Show(string prefabPath, Action<string> done)
		{
			var window = CreateInstance<InsertCloneNameWindow>();
			window.titleContent = new GUIContent("Clone to Library");
			window._prefabPath = prefabPath;
			window._name = Path.GetFileNameWithoutExtension(prefabPath);
			window._done = done;
			window.minSize = window.maxSize = new Vector2(420, 96);
			window.ShowUtility();
			window.CenterOnMainWin();
		}

		private void CenterOnMainWin()
		{
			var main = EditorGUIUtility.GetMainWindowPosition();
			position = new Rect(main.x + (main.width - 420) / 2, main.y + (main.height - 96) / 2, 420, 96);
		}

		private void OnGUI()
		{
			var submit = Event.current.type == EventType.KeyDown && Event.current.keyCode is KeyCode.Return or KeyCode.KeypadEnter;
			if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) {
				Close();
				GUIUtility.ExitGUI();
			}
			EditorGUILayout.LabelField("Name of the new insert, which gets a folder of its own:");
			GUI.SetNextControlName("Name");
			_name = EditorGUILayout.TextField(_name);
			if (!_focused) {
				// The source's name, selected, to be typed over or changed.
				EditorGUI.FocusTextInControl("Name");
				if (GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) is TextEditor editor) {
					editor.SelectAll();
				}
				_focused = Event.current.type == EventType.Repaint;
			}
			var error = InsertLibraryClone.CannotName(_prefabPath, _name);
			EditorGUILayout.LabelField(_error ?? (error != null && _name != Path.GetFileNameWithoutExtension(_prefabPath) ? error : " "), EditorStyles.miniLabel);
			using (new EditorGUILayout.HorizontalScope()) {
				GUILayout.FlexibleSpace();
				if (GUILayout.Button("Cancel", GUILayout.Width(80))) {
					Close();
					GUIUtility.ExitGUI();
				}
				using (new EditorGUI.DisabledScope(error != null)) {
					if (GUILayout.Button("Clone", GUILayout.Width(80)) || submit && error == null) {
						try {
							_done(_name);
							Close();
						} catch (Exception e) {
							_error = e.Message;
							Debug.LogException(e);
						}
						GUIUtility.ExitGUI();
					}
				}
			}
		}
	}
}
