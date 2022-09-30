// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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
				var previewSize = resolvedStyle.width
				                  - resolvedStyle.paddingLeft - resolvedStyle.paddingRight
				                  - resolvedStyle.borderLeftWidth - resolvedStyle.borderRightWidth;
				var rect = EditorGUILayout.GetControlRect(false, previewSize, GUILayout.Width(previewSize));
				_previewEditor.OnInteractivePreviewGUI(rect, GUI.skin.box);
				_container.style.height = _container.resolvedStyle.width;
			}
		}
	}
}
