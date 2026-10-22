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

using System;
using System.IO;
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
		private SerializedProperty _nudgeTimeProperty;
		private SerializedProperty _metadataProperty;
		private bool _packageFoldout;
		private bool _runtimeCompressSideChannelTextures = true;
		private bool _runtimeCompressNormalMaps = true;

		private const string RuntimeCompressSideChannelTexturesKey =
			"VisualPinball.Unity.Editor.TableInspector.RuntimeCompressSideChannelTextures";
		private const string RuntimeCompressNormalMapsKey =
			"VisualPinball.Unity.Editor.TableInspector.RuntimeCompressNormalMaps";

		protected override void OnEnable()
		{
			base.OnEnable();

			_globalDifficultyProperty = serializedObject.FindProperty(nameof(TableComponent.GlobalDifficulty));
			_nudgeTimeProperty = serializedObject.FindProperty(nameof(TableComponent.NudgeTime));
			_metadataProperty = serializedObject.FindProperty(nameof(TableComponent.Metadata));
			_runtimeCompressSideChannelTextures = EditorPrefs.GetBool(RuntimeCompressSideChannelTexturesKey, true);
			_runtimeCompressNormalMaps = EditorPrefs.GetBool(RuntimeCompressNormalMapsKey, true);
		}

		public override void OnInspectorGUI()
		{
			var tableComponent = (TableComponent) target;

			BeginEditing();

			PropertyField(_globalDifficultyProperty);
			PropertyField(_nudgeTimeProperty);

			EditorGUILayout.Space();
			_packageFoldout = EditorGUILayout.Foldout(_packageFoldout, "Package", true);
			if (_packageFoldout) {
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_metadataProperty, true);
				EditorGUI.indentLevel--;
			}

			EndEditing();
			if (_packageFoldout && targets.Length == 1) TableProvenanceEditor.Draw(tableComponent);

			if (_packageFoldout && !EditorApplication.isPlaying) {
				//DrawDefaultInspector();
				const string ext = "vpe";
				EditorGUILayout.HelpBox(
					$"Screenshot preset: {PackageScreenshotGenerator.PortraitWidth}x{PackageScreenshotGenerator.PortraitHeight} PNG using a computed top-down shot at {PackageScreenshotGenerator.FieldOfView:F1} deg FOV.",
					MessageType.None);
				EditorGUI.BeginChangeCheck();
				_runtimeCompressSideChannelTextures = EditorGUILayout.ToggleLeft(
					"Compress sidecar textures (Unity runtime compression)",
					_runtimeCompressSideChannelTextures);
				if (EditorGUI.EndChangeCheck()) {
					EditorPrefs.SetBool(RuntimeCompressSideChannelTexturesKey, _runtimeCompressSideChannelTextures);
				}
				EditorGUI.BeginChangeCheck();
				_runtimeCompressNormalMaps = EditorGUILayout.ToggleLeft(
					"Compress runtime normal maps (Unity runtime compression)",
					_runtimeCompressNormalMaps);
				if (EditorGUI.EndChangeCheck()) {
					EditorPrefs.SetBool(RuntimeCompressNormalMapsKey, _runtimeCompressNormalMaps);
				}
				if (GUILayout.Button("Generate Screenshot")) {
					// Run the capture *outside* OnInspectorGUI. Rendering
					// (SubmitRenderRequest/ReadPixels) inside the IMGUI event uses the
					// GUI frame's stale culling, so the per-shot lamp/bulb toggles
					// don't take effect and later shots leak the first shot's state.
					var table = tableComponent;
					EditorApplication.delayCall += () => {
						try {
							// Generate runs across several editor frames (it lets HDRP settle
							// lamp state between shots), so the result arrives via callback.
							PackageScreenshotGenerator.Generate(table, @"Assets/Screenshots",
								result => {
									Debug.Log(
										$"Generated package screenshot at {result.AssetPath} " +
										$"(camera Y: {result.CameraPosition.y:F3}m, distance: {result.CameraDistance:F3}m, " +
										table);

									var screenshot = AssetDatabase.LoadAssetAtPath<Texture2D>(result.AssetPath);
									if (screenshot) {
										Selection.activeObject = screenshot;
										EditorGUIUtility.PingObject(screenshot);
									}
								},
								exception => {
									Debug.LogException(exception, table);
									EditorUtility.DisplayDialog("Generate Screenshot Failed", exception.Message, "OK");
								});
						} catch (Exception exception) {
							Debug.LogException(exception, table);
							EditorUtility.DisplayDialog("Generate Screenshot Failed", exception.Message, "OK");
						}
					};
				}
				if (GUILayout.Button($"Save as .{ext}")) {
					var tableContainer = tableComponent.TableContainer;
					var path = EditorUtility.SaveFilePanel(
						$"Save table as .{ext}",
						"",
						tableComponent.name + $".{ext}",
						ext);

					if (!string.IsNullOrEmpty(path)) {
						// Run outside OnInspectorGUI: the export yields between stages and offloads the
						// texture byte-load to worker threads, driven by a cancelable progress bar.
						var tableGo = tableComponent.gameObject;
						var compSide = _runtimeCompressSideChannelTextures;
						var compNormal = _runtimeCompressNormalMaps;
						EditorApplication.delayCall += async () => {
							var writer = new PackageWriter(tableGo, compSide, compNormal, @"Assets/Screenshots");
							await VpeExportRunner.RunAsync(writer, path, $"Saving {Path.GetFileName(path)}");
						};
					}
				}
			}
		}
	}
}
