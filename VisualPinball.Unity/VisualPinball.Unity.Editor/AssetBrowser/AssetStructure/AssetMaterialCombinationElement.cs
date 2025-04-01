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
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public class AssetMaterialCombinationElement: VisualElement
	{
		public bool Enabled { set => _toggle.SetValueWithoutNotify(value); }
		public string Name => Combination.Name;
		public event EventHandler<bool> OnClicked;

		private readonly ToolbarToggle _toggle;

		public readonly AssetMaterialCombination Combination;

		public AssetMaterialCombinationElement(AssetMaterialCombination combination, Asset asset)
		{
			Combination = combination;

			var ui = new VisualElement();
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetMaterialCombinationElement.uxml");
			visualTree.CloneTree(ui);

			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetMaterialCombinationElement.uss");
			styleSheets.Add(styleSheet);

			_toggle = ui.Q<ToolbarToggle>("toggle");
			_toggle.RegisterValueChangedCallback(val => OnClicked?.Invoke(this, val.newValue));

			ui.Q<Label>("label").text = Name;

			var thumbPath = $"{asset.Library.ThumbnailRoot}/{Combination.ThumbId}.png";
			if (File.Exists(thumbPath)) {
				var tex = new Texture2D(AssetBrowser.ThumbSize, AssetBrowser.ThumbSize);
				tex.LoadImage(File.ReadAllBytes(thumbPath));
				ui.Q<Image>("thumbnail").image = tex;
			}

			Add(ui);
		}
	}
}
