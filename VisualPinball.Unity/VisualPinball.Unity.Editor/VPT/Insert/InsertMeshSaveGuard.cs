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

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Keeps the meshes inserts generate out of saved scenes. New ones are never
	/// saved; one that an older version saved into the scene, still held by an
	/// insert that hasn't rebuilt since, is let go of when the scene is saved.
	/// </summary>
	[InitializeOnLoad]
	internal static class InsertMeshSaveGuard
	{
		static InsertMeshSaveGuard()
		{
			EditorSceneManager.sceneSaving -= OnSceneSaving;
			EditorSceneManager.sceneSaving += OnSceneSaving;
		}

		private static void OnSceneSaving(Scene scene, string path)
		{
			foreach (var root in scene.GetRootGameObjects()) {
				foreach (var insert in root.GetComponentsInChildren<InsertComponent>(true)) {
					insert.UnsaveLegacyMesh();
					UnsaveOverriddenMesh(insert);
				}
			}
		}

		/// <summary>
		/// A prefab instance records the mesh as an override. Once the insert
		/// rebuilt its own, which isn't saved, the override keeps pointing at the
		/// old mesh, and that would be written into the scene again.
		/// </summary>
		private static void UnsaveOverriddenMesh(InsertComponent insert)
		{
			if (!PrefabUtility.IsPartOfPrefabInstance(insert)) {
				return;
			}
			var modifications = PrefabUtility.GetPropertyModifications(insert.gameObject);
			if (modifications == null) {
				return;
			}
			foreach (var modification in modifications) {
				if (modification.propertyPath == "m_Mesh" && modification.objectReference is Mesh mesh) {
					InsertComponent.UnsaveLegacyMesh(mesh);
				}
			}
		}
	}
}
