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
using UnityEngine.SceneManagement;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Scenes saved before mesh components tracked which renderer they hid turned the
	/// renderer off whenever a mesh GameObject was deactivated. When such a scene opens,
	/// the mesh components on inactive GameObjects take those renderers over, so they show
	/// again when the GameObject is activated, as they did before.
	/// </summary>
	[InitializeOnLoad]
	internal static class MeshRendererStateMigration
	{
		static MeshRendererStateMigration()
		{
			EditorSceneManager.sceneOpened += (scene, _) => Migrate(scene);
			EditorApplication.delayCall += () => {
				if (EditorApplication.isPlayingOrWillChangePlaymode) {
					return;
				}
				for (var i = 0; i < SceneManager.sceneCount; i++) {
					Migrate(SceneManager.GetSceneAt(i));
				}
			};
		}

		internal static void Migrate(Scene scene)
		{
			if (!scene.isLoaded) {
				return;
			}
			foreach (var root in scene.GetRootGameObjects()) {
				foreach (var state in root.GetComponentsInChildren<IMeshRendererState>(true)) {
					// a prefab instance only saves its overrides: the old code never kept its renderer off
					if (state is UnityEngine.Object obj && PrefabUtility.IsPartOfPrefabInstance(obj)) {
						continue;
					}
					state.MigrateInactiveLegacyRenderer();
				}
			}
		}
	}
}
