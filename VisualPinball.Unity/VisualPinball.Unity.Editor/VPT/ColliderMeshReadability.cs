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
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VisualPinball.Unity.Playfield;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Keeps imported meshes that VPE reads while generating colliders CPU-readable.
	/// Model reimports are deferred when requested from an inspector, and deduplicated
	/// by asset path because one model can provide meshes to many collider components.
	/// </summary>
	[InitializeOnLoad]
	internal static class ColliderMeshReadability
	{
		private static readonly HashSet<string> PendingAssetPaths = new(StringComparer.Ordinal);
		private static readonly HashSet<string> ReportedUnfixableMeshes = new(StringComparer.Ordinal);
		private static bool _applyScheduled;
		private static bool _sceneScanScheduled;

		static ColliderMeshReadability()
		{
			EditorApplication.delayCall += ScheduleLoadedSceneScan;
			EditorApplication.hierarchyChanged += ScheduleLoadedSceneScan;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
			EditorSceneManager.sceneOpened += OnSceneOpened;
			PrefabStage.prefabStageOpened += OnPrefabStageOpened;
		}

		/// <summary>
		/// Called after an inspector applies changes. Only collider consumers on the
		/// same GameObject are inspected, so ordinary render meshes remain untouched.
		/// </summary>
		internal static void QueueFor(Component component)
		{
			if (!component) {
				return;
			}

			var paths = new HashSet<string>(StringComparer.Ordinal);
			foreach (var collider in component.GetComponents<ICollidableComponent>()) {
				CollectAssetPaths(collider, paths);
			}
			Queue(paths);
		}

		/// <summary>
		/// Synchronously enables Read/Write for the supplied meshes. Package export
		/// uses this because it must consume the mesh data before returning control
		/// to the editor update loop.
		/// </summary>
		internal static int EnsureReadableImmediately(IEnumerable<Mesh> meshes)
		{
			var paths = new HashSet<string>(StringComparer.Ordinal);
			foreach (var mesh in meshes) {
				CollectAssetPath(mesh, paths, null);
			}
			return Apply(paths);
		}

		/// <summary>
		/// Synchronously validates every VPE collider in a hierarchy, including
		/// explicit target meshes, primitive render meshes, and custom playfields.
		/// </summary>
		internal static int EnsureReadableImmediately(GameObject root)
		{
			if (!root) {
				return 0;
			}

			var paths = new HashSet<string>(StringComparer.Ordinal);
			foreach (var collider in root.GetComponentsInChildren<ICollidableComponent>(true)) {
				CollectAssetPaths(collider, paths);
			}
			return Apply(paths);
		}

		private static void CollectAssetPaths(ICollidableComponent collider, ISet<string> paths)
		{
			var component = collider as Component;
			if (!component) {
				return;
			}

			if (collider is IColliderMesh colliderMesh) {
				for (var index = 0; index < colliderMesh.NumColliderMeshes; index++) {
					CollectAssetPath(colliderMesh.GetColliderMesh(index), paths, component);
				}
			}

			// Visible primitive meshes are deliberately omitted from IColliderMesh to
			// avoid duplicating them in colliders.glb, but physics still reads them.
			if (collider is PrimitiveColliderComponent primitiveCollider && primitiveCollider.MainComponent) {
				CollectAssetPath(primitiveCollider.MainComponent.GetUnityMesh(), paths, component);
			}

			// A custom playfield uses its authored render mesh as collision geometry.
			if (collider is PlayfieldColliderComponent) {
				var playfieldMesh = component.GetComponent<PlayfieldMeshComponent>();
				if (playfieldMesh && !playfieldMesh.AutoGenerate) {
					CollectAssetPath(component.GetComponent<MeshFilter>()?.sharedMesh, paths, component);
				}
			}
		}

		private static void CollectAssetPath(Mesh mesh, ISet<string> paths, Object context)
		{
			if (!mesh || mesh.isReadable) {
				return;
			}

			var assetPath = AssetDatabase.GetAssetPath(mesh);
			if (string.IsNullOrEmpty(assetPath)) {
				ReportUnfixable(mesh, context, "it is not backed by an imported model asset");
				return;
			}

			if (!(AssetImporter.GetAtPath(assetPath) is ModelImporter modelImporter)) {
				ReportUnfixable(mesh, context, $"'{assetPath}' does not use a ModelImporter");
				return;
			}

			if (!modelImporter.isReadable) {
				paths.Add(assetPath);
			}
		}

		private static void ReportUnfixable(Mesh mesh, Object context, string reason)
		{
			var assetPath = AssetDatabase.GetAssetPath(mesh);
			var key = string.IsNullOrEmpty(assetPath) ? $"instance:{mesh.GetEntityId()}" : assetPath;
			if (!ReportedUnfixableMeshes.Add(key)) {
				return;
			}
			Debug.LogError($"Mesh '{mesh.name}' is not CPU-readable and VPE cannot enable Read/Write automatically because {reason}.", context);
		}

		private static void Queue(IEnumerable<string> paths)
		{
			foreach (var path in paths) {
				PendingAssetPaths.Add(path);
			}
			if (PendingAssetPaths.Count == 0 || _applyScheduled) {
				return;
			}

			_applyScheduled = true;
			EditorApplication.delayCall += ApplyPending;
		}

		private static void ApplyPending()
		{
			_applyScheduled = false;
			if (PendingAssetPaths.Count == 0) {
				return;
			}

			if (EditorApplication.isCompiling || EditorApplication.isUpdating) {
				Queue(Array.Empty<string>());
				return;
			}

			var paths = new HashSet<string>(PendingAssetPaths, StringComparer.Ordinal);
			PendingAssetPaths.Clear();
			Apply(paths);
		}

		private static int Apply(IEnumerable<string> paths)
		{
			var changed = 0;
			foreach (var assetPath in paths) {
				if (!(AssetImporter.GetAtPath(assetPath) is ModelImporter modelImporter) || modelImporter.isReadable) {
					continue;
				}

				try {
					modelImporter.isReadable = true;
					modelImporter.SaveAndReimport();
					changed++;
					Debug.Log($"Enabled Read/Write for mesh asset '{assetPath}' required by VPE.");
				} catch (Exception exception) {
					Debug.LogError($"Could not enable Read/Write for mesh asset '{assetPath}' required by VPE: {exception.Message}");
				}
			}
			return changed;
		}

		private static void ScheduleLoadedSceneScan()
		{
			if (_sceneScanScheduled || EditorApplication.isPlayingOrWillChangePlaymode) {
				return;
			}
			_sceneScanScheduled = true;
			EditorApplication.delayCall += ScanLoadedScenes;
		}

		private static void ScanLoadedScenes()
		{
			_sceneScanScheduled = false;
			if (EditorApplication.isCompiling || EditorApplication.isUpdating) {
				ScheduleLoadedSceneScan();
				return;
			}

			var paths = new HashSet<string>(StringComparer.Ordinal);
			for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++) {
				var scene = SceneManager.GetSceneAt(sceneIndex);
				if (!scene.isLoaded) {
					continue;
				}
				foreach (var root in scene.GetRootGameObjects()) {
					foreach (var collider in root.GetComponentsInChildren<ICollidableComponent>(true)) {
						CollectAssetPaths(collider, paths);
					}
				}
			}
			Apply(paths);
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state != PlayModeStateChange.ExitingEditMode) {
				return;
			}
			ApplyPending();
			ScanLoadedScenes();
		}

		private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => ScheduleLoadedSceneScan();

		private static void OnPrefabStageOpened(PrefabStage stage) => ScheduleLoadedSceneScan();
	}
}
