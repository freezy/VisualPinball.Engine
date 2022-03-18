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

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public partial class AssetBrowserX
	{
		private ToolbarButton _refreshButton;
		private VisualElement _leftPane;
		private VisualElement _midPane;
		private Label _bottomLabel;
		private Slider _sizeSlider;

		private static readonly Dictionary<string, Type> Types = new();

		public void CreateGUI()
		{
			// import UXML
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetBrowserX.uxml");
			visualTree.CloneTree(rootVisualElement);

			var ui = rootVisualElement;

			// import style sheet
			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetBrowserX.uss");
			ui.styleSheets.Add(styleSheet);

			_leftPane = ui.Q<VisualElement>("leftPane");
			_midPane = ui.Q<VisualElement>("midPane");

			_bottomLabel = ui.Q<Label>("bottomLabel");
			_sizeSlider = ui.Q<Slider>("sizeSlider");
			_sizeSlider.value = _thumbnailSize;
			_sizeSlider.RegisterValueChangedCallback(OnThumbSizeChanged);

			_refreshButton = ui.Q<ToolbarButton>("refreshButton");
			_refreshButton.clicked += Refresh;

			_midPane.RegisterCallback<DragUpdatedEvent>(OnDragUpdatedEvent);
			_midPane.RegisterCallback<DragPerformEvent>(OnDragPerformEvent);

			Init();
		}

		private void OnDestroy()
		{
			_sizeSlider.UnregisterValueChangedCallback(OnThumbSizeChanged);

			_midPane.UnregisterCallback<DragPerformEvent>(OnDragPerformEvent);
			_midPane.UnregisterCallback<DragUpdatedEvent>(OnDragUpdatedEvent);
			_refreshButton.clicked -= Refresh;

			foreach (var assetLibrary in _assetLibraries) {
				assetLibrary.Dispose();
			}
		}

		private void OnAssetSelectionChange(IEnumerable<object> selectedItems)
		{
			// // Clear all previous content from the pane
			// _rightPane?.Clear();
			//
			// // Get the selected asset
			// var selectedTexture = selectedItems.First() as Texture;
			// if (selectedTexture == null) {
			// 	return;
			// }
			//
			// // Add a new Image control and display the asset
			// var spriteImage = new Image {
			// 	scaleMode = ScaleMode.ScaleToFit,
			// 	image = selectedTexture
			// };
			//
			// // Add the Image control to the right-hand pane
			// _rightPane?.Add(spriteImage);
		}

		private static Type TypeByName(string name)
		{
			if (Types.ContainsKey(name)) {
				return Types[name];
			}
			Types[name] = AppDomain.CurrentDomain.GetAssemblies().Reverse().Select(assembly => assembly.GetType(name)).FirstOrDefault(tt => tt != null);
			return Types[name];
		}
	}
}
