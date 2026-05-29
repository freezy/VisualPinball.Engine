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
		private string _screenshotHdriPath;
		private Cubemap _screenshotHdri;
		private float _screenshotHdriExposure;

		private const string RuntimeCompressSideChannelTexturesKey =
			"VisualPinball.Unity.Editor.TableInspector.RuntimeCompressSideChannelTextures";
		private const string CompressGltfTexturesKey =
			"VisualPinball.Unity.Editor.TableInspector.CompressGltfTextures";
		private const string RuntimeCompressNormalMapsKey =
			"VisualPinball.Unity.Editor.TableInspector.RuntimeCompressNormalMaps";
		private const string ScreenshotHdriPathKey =
			"VisualPinball.Unity.Editor.TableInspector.ScreenshotHdriPath";
		private const string ScreenshotHdriExposureKey =
			"VisualPinball.Unity.Editor.TableInspector.ScreenshotHdriExposure";

		protected override void OnEnable()
		{
			base.OnEnable();

			_globalDifficultyProperty = serializedObject.FindProperty(nameof(TableComponent.GlobalDifficulty));
			_metadataProperty = serializedObject.FindProperty(nameof(TableComponent.Metadata));
			_runtimeCompressSideChannelTextures = EditorPrefs.GetBool(RuntimeCompressSideChannelTexturesKey, true);
			_compressGltfTextures = EditorPrefs.GetBool(CompressGltfTexturesKey, true);
			_runtimeCompressNormalMaps = EditorPrefs.GetBool(RuntimeCompressNormalMapsKey, true);
			_screenshotHdriPath = EditorPrefs.GetString(ScreenshotHdriPathKey, PackageScreenshotGenerator.DefaultHdriAssetPath);
			_screenshotHdri = AssetDatabase.LoadAssetAtPath<Cubemap>(_screenshotHdriPath);
			_screenshotHdriExposure = EditorPrefs.GetFloat(ScreenshotHdriExposureKey, 4f);
			if (!_screenshotHdri && _screenshotHdriPath != PackageScreenshotGenerator.DefaultHdriAssetPath) {
				_screenshotHdriPath = PackageScreenshotGenerator.DefaultHdriAssetPath;
				_screenshotHdri = AssetDatabase.LoadAssetAtPath<Cubemap>(_screenshotHdriPath);
			}
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
				EditorGUILayout.HelpBox(
					$"Screenshot preset: {PackageScreenshotGenerator.PortraitWidth}x{PackageScreenshotGenerator.PortraitHeight} PNG using a computed top-down shot at {PackageScreenshotGenerator.FieldOfView:F1} deg FOV.",
					MessageType.None);
				EditorGUI.BeginChangeCheck();
				_screenshotHdri = (Cubemap)EditorGUILayout.ObjectField("Screenshot HDRI", _screenshotHdri, typeof(Cubemap), false);
				if (EditorGUI.EndChangeCheck()) {
					_screenshotHdriPath = _screenshotHdri ? AssetDatabase.GetAssetPath(_screenshotHdri) : string.Empty;
					EditorPrefs.SetString(ScreenshotHdriPathKey, _screenshotHdriPath);
				}
				EditorGUI.BeginChangeCheck();
				_screenshotHdriExposure = EditorGUILayout.FloatField("Screenshot HDRI Exposure", _screenshotHdriExposure);
				if (EditorGUI.EndChangeCheck()) {
					EditorPrefs.SetFloat(ScreenshotHdriExposureKey, _screenshotHdriExposure);
				}
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
				if (GUILayout.Button("Generate Screenshot")) {
					if (!_screenshotHdri) {
						EditorUtility.DisplayDialog("Generate Screenshot Failed",
							"Please assign a screenshot HDRI cubemap before generating a screenshot.", "OK");
					} else {
						// Run the capture *outside* OnInspectorGUI. Rendering
						// (SubmitRenderRequest/ReadPixels) inside the IMGUI event uses the
						// GUI frame's stale culling, so the per-shot lamp/bulb toggles
						// don't take effect and later shots leak the first shot's state.
						var table = tableComponent;
						var hdri = _screenshotHdri;
						var exposure = _screenshotHdriExposure;
						EditorApplication.delayCall += () => {
							try {
								// Generate runs across several editor frames (it lets HDRP settle
								// lamp state between shots), so the result arrives via callback.
								PackageScreenshotGenerator.Generate(table, @"Assets/Screenshots", hdri, exposure,
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
							_runtimeCompressNormalMaps,
							@"Assets/Screenshots");
						writer.WritePackageSync(path);
					}
				}
			}
		}
	}
}