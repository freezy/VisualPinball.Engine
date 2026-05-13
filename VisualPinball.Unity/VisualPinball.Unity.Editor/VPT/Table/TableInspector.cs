// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedType.Global

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(TableComponent))]
	[CanEditMultipleObjects]
	public class TableInspector : ItemInspector
	{
		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		private SerializedProperty _globalDifficultyProperty;
		private SerializedProperty _metadataProperty;
		private bool _packageFoldout;
		private bool _runtimeCompressSideChannelTextures = true;
		private bool _compressGltfTextures = true;
		private bool _runtimeCompressNormalMaps = true;

		private const string RuntimeCompressSideChannelTexturesKey =
			"VisualPinball.Unity.Editor.TableInspector.RuntimeCompressSideChannelTextures";
		private const string CompressGltfTexturesKey =
			"VisualPinball.Unity.Editor.TableInspector.CompressGltfTextures";
		private const string RuntimeCompressNormalMapsKey =
			"VisualPinball.Unity.Editor.TableInspector.RuntimeCompressNormalMaps";

		protected override void OnEnable()
		{
			base.OnEnable();

			_globalDifficultyProperty = serializedObject.FindProperty(nameof(TableComponent.GlobalDifficulty));
			_metadataProperty = serializedObject.FindProperty(nameof(TableComponent.Metadata));
			_runtimeCompressSideChannelTextures = EditorPrefs.GetBool(RuntimeCompressSideChannelTexturesKey, true);
			_compressGltfTextures = EditorPrefs.GetBool(CompressGltfTexturesKey, true);
			_runtimeCompressNormalMaps = EditorPrefs.GetBool(RuntimeCompressNormalMapsKey, true);
		}

		public override void OnInspectorGUI()
		{
			var tableComponent = (TableComponent) target;

			BeginEditing();

			PropertyField(_globalDifficultyProperty);

			EditorGUILayout.Space();
			_packageFoldout = EditorGUILayout.Foldout(_packageFoldout, "Package", true);
			if (_packageFoldout) {
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_metadataProperty, true);
				EditorGUI.indentLevel--;
			}

			EndEditing();

			if (_packageFoldout && !EditorApplication.isPlaying) {
				//DrawDefaultInspector();
				const string ext = "vpe";
				EditorGUI.BeginChangeCheck();
				_runtimeCompressSideChannelTextures = EditorGUILayout.ToggleLeft(
					"Compress sidecar textures (Unity runtime compression)",
					_runtimeCompressSideChannelTextures);
				if (EditorGUI.EndChangeCheck()) {
					EditorPrefs.SetBool(RuntimeCompressSideChannelTexturesKey, _runtimeCompressSideChannelTextures);
				}
				EditorGUI.BeginChangeCheck();
				_compressGltfTextures = EditorGUILayout.ToggleLeft(
					"Compress glTF textures",
					_compressGltfTextures);
				if (EditorGUI.EndChangeCheck()) {
					EditorPrefs.SetBool(CompressGltfTexturesKey, _compressGltfTextures);
				}
				EditorGUI.BeginChangeCheck();
				_runtimeCompressNormalMaps = EditorGUILayout.ToggleLeft(
					"Compress runtime normal maps (Unity runtime compression)",
					_runtimeCompressNormalMaps);
				if (EditorGUI.EndChangeCheck()) {
					EditorPrefs.SetBool(RuntimeCompressNormalMapsKey, _runtimeCompressNormalMaps);
				}
				if (GUILayout.Button($"Save as .{ext}")) {
					var tableContainer = tableComponent.TableContainer;
					var path = EditorUtility.SaveFilePanel(
						$"Save table as .{ext}",
						"",
						tableComponent.name + $".{ext}",
						ext);

					if (!string.IsNullOrEmpty(path)) {
						var writer = new PackageWriter(
							tableComponent.gameObject,
							_runtimeCompressSideChannelTextures,
							_compressGltfTextures,
							_runtimeCompressNormalMaps);
						writer.WritePackageSync(path);
					}
				}
			}
		}
	}
}
