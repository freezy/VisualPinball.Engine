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

using System;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Image = UnityEngine.UIElements.Image;

namespace VisualPinball.Unity.Editor
{
	public class AssetMaterialCombinationElement: VisualElement
	{
		public bool Enabled { set => _toggle.SetValueWithoutNotify(value); }
		public string Name => _isDecalVariation ? Combination.Name : Combination.MaterialName;
		public event EventHandler<bool> OnClicked;

		public readonly AssetMaterialCombination Combination;

		private readonly ToolbarToggle _toggle;
		private readonly bool _isDecalVariation;

		public AssetMaterialCombinationElement(AssetMaterialCombination combination, Asset asset, bool isDecalVariation)
		{
			Combination = combination;
			_isDecalVariation = isDecalVariation;

			var ui = new VisualElement();
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetMaterialCombinationElement.uxml");
			visualTree.CloneTree(ui);

			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetMaterialCombinationElement.uss");
			styleSheets.Add(styleSheet);

			_toggle = ui.Q<ToolbarToggle>("toggle");
			_toggle.RegisterValueChangedCallback(val => OnClicked?.Invoke(this, val.newValue));

			ui.Q<Label>("label").text = Name;
			if (File.Exists(Combination.ThumbPath)) {
				var tex = asset.LoadThumbTexture(Combination.ThumbPath);
				ui.Q<Image>("thumbnail").image = tex;
			}
			Add(ui);
		}
	}
}
