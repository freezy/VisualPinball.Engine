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

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// The 3D view of an asset at the top of its details, turned with the
	/// mouse. A prefab is rendered in a preview scene the render pipeline sets
	/// up as a table would be lit and exposed (<see cref="AssetPreviewEnvironmentProvider"/>),
	/// so a lit insert glows through its cover instead of showing up as a dark
	/// disc on a white drum; anything else, or any asset without a pipeline
	/// environment, gets Unity's own interactive preview.
	/// </summary>
	[UxmlElement]
	public partial class PreviewEditorElement : VisualElement
	{
		public Object Object;

		private const float FieldOfView = 30f;
		private const float Padding = 1.4f;

		private readonly IMGUIContainer _container;
		private UnityEditor.Editor _previewEditor;

		private PreviewRenderUtility _utility;
		private IAssetPreviewEnvironment _environment;
		private GameObject _instance;
		private Object _instanceOf;
		private Bounds _bounds;
		private Vector2 _angles = new(30f, 30f);

		public PreviewEditorElement()
		{
			_container = new IMGUIContainer();
			_container.onGUIHandler = OnGUI;
			_container.style.height = _container.resolvedStyle.width;
			Add(_container);
			RegisterCallback<DetachFromPanelEvent>(_ => Release());
		}

		/// <summary>
		/// Renders the asset as the element shows it, for a thumbnail or a test,
		/// from the given pitch and yaw in degrees.
		/// </summary>
		public static Texture2D RenderStill(Object asset, int size, float pitch = 30f, float yaw = 30f)
		{
			var element = new PreviewEditorElement { Object = asset, _angles = new Vector2(pitch, yaw) };
			try {
				if (!element.PrepareScene()) {
					return null;
				}
				element._utility.BeginStaticPreview(new Rect(0, 0, size, size));
				element.RenderScene();
				var rendered = element._utility.EndStaticPreview();
				// The utility's texture goes with it: keep a copy.
				var copy = new Texture2D(rendered.width, rendered.height, rendered.format, false);
				Graphics.CopyTexture(rendered, copy);
				return copy;
			} finally {
				element.Release();
			}
		}

		private void OnGUI()
		{
			var previewSize = resolvedStyle.width
			                  - resolvedStyle.paddingLeft - resolvedStyle.paddingRight
			                  - resolvedStyle.borderLeftWidth - resolvedStyle.borderRightWidth;
			var rect = EditorGUILayout.GetControlRect(false, previewSize, GUILayout.Width(previewSize));
			_container.style.height = _container.resolvedStyle.width;

			if (AssetPreviewEnvironmentProvider.IsAvailable && Object is GameObject && PrepareScene()) {
				DrawScene(rect);
				return;
			}
			DrawEditorPreview(rect);
		}

		#region Pipeline preview

		/// <summary>Instantiates the asset into the preview scene, once per asset.</summary>
		private bool PrepareScene()
		{
			if (_utility == null) {
				_utility = new PreviewRenderUtility();
				_utility.camera.fieldOfView = FieldOfView;
				_utility.camera.nearClipPlane = 0.001f;
				_utility.camera.farClipPlane = 100f;
				_utility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
				_utility.lights[1].transform.rotation = Quaternion.Euler(340f, 218f, 177f);
				_utility.ambientColor = new Color(0.2f, 0.2f, 0.2f, 0f);
				_environment = AssetPreviewEnvironmentProvider.Create(_utility);
			}
			if (_instance != null && _instanceOf == Object) {
				return true;
			}
			if (_instance != null) {
				Object.DestroyImmediate(_instance);
				_instance = null;
			}
			_instanceOf = Object;
			if (Object is not GameObject prefab) {
				return false;
			}
			_instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
			if (_instance == null) {
				return false;
			}
			_instance.hideFlags = HideFlags.HideAndDontSave;
			_utility.AddSingleGO(_instance);
			var renderers = _instance.GetComponentsInChildren<Renderer>();
			_bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(_instance.transform.position, Vector3.one * 0.1f);
			foreach (var renderer in renderers) {
				_bounds.Encapsulate(renderer.bounds);
			}
			return true;
		}

		private void DrawScene(Rect rect)
		{
			var evt = Event.current;
			var controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
			switch (evt.GetTypeForControl(controlId)) {
				case EventType.MouseDown when rect.Contains(evt.mousePosition) && evt.button == 0:
					GUIUtility.hotControl = controlId;
					evt.Use();
					break;
				case EventType.MouseDrag when GUIUtility.hotControl == controlId:
					_angles.y += evt.delta.x;
					_angles.x = Mathf.Clamp(_angles.x + evt.delta.y, -89f, 89f);
					evt.Use();
					_container.MarkDirtyRepaint();
					break;
				case EventType.MouseUp when GUIUtility.hotControl == controlId:
					GUIUtility.hotControl = 0;
					evt.Use();
					break;
				case EventType.Repaint:
					_utility.BeginPreview(rect, GUIStyle.none);
					RenderScene();
					var rendered = _utility.EndPreview();
					GUI.DrawTexture(rect, rendered, ScaleMode.StretchToFill, false);
					break;
			}
		}

		/// <summary>Points the camera at the asset from the current angles and renders.</summary>
		private void RenderScene()
		{
			var camera = _utility.camera;
			var rotation = Quaternion.Euler(_angles.x, _angles.y, 0f);
			var distance = _bounds.extents.magnitude / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * Padding;
			camera.transform.rotation = rotation;
			camera.transform.position = _bounds.center - rotation * Vector3.forward * distance;
			_environment?.BeginRender();
			_utility.Render(true);
			_environment?.EndRender();
		}

		private void Release()
		{
			if (_previewEditor != null) {
				Object.DestroyImmediate(_previewEditor);
				_previewEditor = null;
			}
			if (_instance != null) {
				Object.DestroyImmediate(_instance);
				_instance = null;
			}
			_environment?.Dispose();
			_environment = null;
			_utility?.Cleanup();
			_utility = null;
		}

		#endregion

		#region Unity's preview

		private void DrawEditorPreview(Rect rect)
		{
			if (_previewEditor == null || Object != _previewEditor.target) {
				if (_previewEditor != null) {
					Object.DestroyImmediate(_previewEditor);
				}
				_previewEditor = UnityEditor.Editor.CreateEditor(Object);
			}
			if (_previewEditor) {
				_previewEditor.OnInteractivePreviewGUI(rect, GUI.skin.box);
			}
		}

		#endregion
	}
}
