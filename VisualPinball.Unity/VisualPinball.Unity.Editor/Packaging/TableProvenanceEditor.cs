// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	internal static class TableProvenanceEditor
	{
		private static TableComponent _lastTable;
		private static int _lastDirtyCount = -1;
		private static TableProvenance _lastSnapshot;

		public static void Draw(TableComponent table)
		{
			if (!table.HasProvenance) {
				if (GUILayout.Button("Initialize table identity")) {
					Undo.RecordObject(table, "Initialize table identity");
					table.RestoreProvenance(TableProvenance.New("legacyScene"));
					Dirty(table);
				}
				return;
			}
			TableProvenance snapshot;
			try {
				var dirtyCount = EditorUtility.GetDirtyCount(table);
				if (_lastTable != table || dirtyCount != _lastDirtyCount || _lastSnapshot == null) {
					_lastSnapshot = table.Provenance.Snapshot(table.Metadata); _lastTable = table; _lastDirtyCount = dirtyCount;
				}
				snapshot = _lastSnapshot;
			}
			catch (Exception e) { EditorGUILayout.HelpBox(e.Message, MessageType.Error); return; }
			using (new EditorGUI.DisabledScope(true)) {
				EditorGUILayout.TextField("Table UUID", snapshot.TableId);
				EditorGUILayout.TextField("Metadata hash", snapshot.MetadataHash);
				EditorGUILayout.TextField("Ancestry root", snapshot.RootHash);
				foreach (var source in snapshot.Credits) {
					EditorGUILayout.LabelField(snapshot.Sources.Contains(source.RootHash) ? "Direct source" : "Reported ancestor", string.IsNullOrEmpty(source.TableId) ? source.Kind + " (original UUID unknown)" : source.TableId);
					EditorGUILayout.TextField("Source version", source.Version ?? "");
					EditorGUILayout.TextField("Source root", source.RootHash);
					EditorGUILayout.TextField("Primary credits", string.Join(", ", (source.PrimaryAuthors ?? new()).Select(a => a.Name + (string.IsNullOrEmpty(a.VpuHandle) ? "" : " (@" + a.VpuHandle + ")"))));
					EditorGUILayout.TextField("Secondary credits", string.Join(", ", (source.SecondaryAuthors ?? new()).Select(a => a.Name + (string.IsNullOrEmpty(a.VpuHandle) ? "" : " (@" + a.VpuHandle + ")"))));
				}
			}
			if (GUILayout.Button("Bump patch version")) {
				var parts = table.Metadata.Version.Split('.');
				if (ulong.TryParse(parts[2], out var patch) && patch < ulong.MaxValue) {
					Undo.RecordObject(table, "Bump table version"); table.Metadata.Version = parts[0] + "." + parts[1] + "." + (patch + 1); Dirty(table);
				}
			}
			if (snapshot.Kind != "diagnostic" && GUILayout.Button("Create derivative")) {
				var provenance = TableProvenance.Derive(snapshot, table.Metadata);
				var copy = UnityEngine.Object.Instantiate(table.gameObject, table.transform.parent);
				copy.name = table.name + " (derivative)";
				var component = copy.GetComponent<TableComponent>();
				component.Metadata = TableProvenance.CopyMetadata(table.Metadata); component.Metadata.Version = "1.0.0";
				component.RestoreProvenance(provenance);
				Undo.RegisterCreatedObjectUndo(copy, "Create derivative"); Dirty(component); Selection.activeGameObject = copy;
			}
			if (GUILayout.Button("Compare / verify source package…")) {
				var path = EditorUtility.OpenFilePanel("Compare table metadata", "", "vpe");
				if (!string.IsNullOrEmpty(path)) Compare(snapshot, table.Metadata.Version, path);
			}
		}

		private static async void Compare(TableProvenance snapshot, string version, string path)
		{
			try {
				var result = await Task.Run(() => {
					using var storage = PackageApi.StorageManager.OpenStorage(path);
					var other = TableProvenance.Read(storage.GetFolder(PackageApi.TableFolder), out var metadata);
					if (other.RootHash == snapshot.RootHash) return "Equal metadata and ancestry commitments.";
					if (snapshot.Credits.Any(c => c.RootHash == other.RootHash)) return snapshot.VerifyParent(other, metadata) ? "Captured source identity, version, and credits verified. Deeper paths remain unresolved until their packages are supplied." : "The supplied source disagrees with the captured credit records.";
					if (snapshot.TableId == other.TableId) return version == metadata.Version ? "Conflicting release: same table UUID and version, different metadata or ancestry." : "Same table UUID, different release version.";
					if (snapshot.Credits.Any(c => c.TableId == other.TableId && c.Version == metadata.Version)) return "Conflicting source: this UUID and version were recorded with a different ancestry root.";
					if (snapshot.Credits.Any(c => c.TableId == other.TableId)) return "This source table is recorded, but this is a different version. Supply the captured version to verify it.";
					if (snapshot.MetadataHash == other.MetadataHash) return "Equal current metadata, different table identity or ancestry.";
					return snapshot.Credits.Any(c => other.Credits.Any(o => o.RootHash == c.RootHash)) ? "Shared reported ancestor; exact paths are unresolved." : "No recorded common ancestry found.";
				});
				EditorUtility.DisplayDialog("Table comparison", result, "OK");
			} catch (Exception e) { EditorUtility.DisplayDialog("Cannot compare package", e.Message, "OK"); }
		}

		private static string ReleasePath(TableProvenance provenance, string version) => Path.Combine("Library", "VPE", "ExportReleases", provenance.TableId, VpeIntegrity.Hash(System.Text.Encoding.UTF8.GetBytes(version)) + ".json");

		internal static void CheckRelease(TableProvenance provenance, string version)
		{
			try {
				var path = ReleasePath(provenance, version);
				if (File.Exists(path) && VpeIntegrity.Parse<string>(File.ReadAllBytes(path), 1024) != provenance.RootHash)
					Debug.LogWarning($"Table {provenance.TableId} version {version} was exported with different metadata or ancestry. Bump the table version before publishing this update.");
			} catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is Newtonsoft.Json.JsonException || e is InvalidDataException) { Debug.LogWarning("Cannot read previous export receipt: " + e.Message); }
		}

		internal static void RecordRelease(TableProvenance provenance, string version)
		{
			try { VpeIntegrity.AtomicWrite(ReleasePath(provenance, version), VpeIntegrity.Json(provenance.RootHash)); }
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { Debug.LogWarning("Package exported, but its local release receipt could not be saved: " + e.Message); }
		}

		private static void Dirty(TableComponent table) { EditorUtility.SetDirty(table); PrefabUtility.RecordPrefabInstancePropertyModifications(table); EditorSceneManager.MarkSceneDirty(table.gameObject.scene); }

		[MenuItem("Pinball/Cache/Verify VPE package…")]
		private static async void VerifyPackage()
		{
			var path = EditorUtility.OpenFilePanel("Verify VPE package", "", "vpe");
			if (string.IsNullOrEmpty(path)) return;
			try { await Task.Run(() => VpeIntegrity.VerifyFile(path)); EditorUtility.DisplayDialog("Package integrity", "All packaged entry checksums verified.", "OK"); }
			catch (Exception e) { EditorUtility.DisplayDialog("Package integrity failed", e.Message, "OK"); }
		}

		[MenuItem("Pinball/Cache/Verify cooked textures")]
		private static async void VerifyTextures()
		{
			try {
				var failures = await Task.Run(() => VpeSharedTextureCache.VerifyAll());
				EditorUtility.DisplayDialog("Texture integrity", failures == 0 ? "All cached textures verified." : $"{failures} damaged texture objects found; affected textures will be rebuilt when loaded.", "OK");
			} catch (Exception e) { EditorUtility.DisplayDialog("Texture integrity failed", e.Message, "OK"); }
		}

		[MenuItem("Pinball/Cache/Clear cooked textures")]
		private static async void ClearTextures()
		{
			try { await Task.Run(VpeSharedTextureCache.Clear); EditorUtility.DisplayDialog("Texture cache", "Unused cached textures cleared. Source packages and authoring assets are preserved.", "OK"); }
			catch (Exception e) { EditorUtility.DisplayDialog("Cannot clear texture cache", e.Message, "OK"); }
		}

		[MenuItem("Pinball/Cache/Rebuild audio index")]
		private static void RebuildAudio() => VpeAudioAssetIndex.Rebuild();
	}
}
