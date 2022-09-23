using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public class PreviewEditorElement : VisualElement
	{
		public Object Object;

		private readonly IMGUIContainer _container;
		private UnityEditor.Editor _previewEditor;

		public new class UxmlFactory : UxmlFactory<PreviewEditorElement, UxmlTraits> { }

		public PreviewEditorElement()
		{
			_container = new IMGUIContainer();
			_container.onGUIHandler = OnGUI;
			_container.style.height = _container.resolvedStyle.width;
			Add(_container);
		}

		private void OnGUI()
		{
			if (_previewEditor == null || Object != _previewEditor.target) {
				if (_previewEditor != null) {
					Object.DestroyImmediate(_previewEditor);
				}
				_previewEditor = UnityEditor.Editor.CreateEditor(Object);
			}

			if (_previewEditor) {
				var previewSize = parent.resolvedStyle.width - parent.resolvedStyle.paddingLeft - parent.resolvedStyle.paddingRight - 3;
				var rect = EditorGUILayout.GetControlRect(false, previewSize, GUILayout.Width(previewSize));
				_previewEditor.OnInteractivePreviewGUI(rect, GUI.skin.box);
				_container.style.height = _container.resolvedStyle.width;
			}
		}
	}
}
