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
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Image = UnityEngine.UIElements.Image;

namespace VisualPinball.Unity.Editor
{
	public class AssetMaterialCombinationElement : VisualElement
	{
		public bool Enabled { set => _toggle.SetValueWithoutNotify(value); }
		public string Name => Combination.Name; //_isDecalVariation ? Combination.Name : Combination.MaterialName;

		public event EventHandler<bool> OnClicked;

		public readonly AssetMaterialCombination Combination;

		private readonly ToolbarToggle _toggle;
		private bool _dragReady;
		private Vector3 _dragStart;

		public AssetMaterialCombinationElement(AssetMaterialCombination combination, Asset asset, string name)
		{
			Combination = combination;

			var ui = new VisualElement();
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetMaterialCombinationElement.uxml");
			visualTree.CloneTree(ui);

			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetMaterialCombinationElement.uss");
			styleSheets.Add(styleSheet);

			_toggle = ui.Q<ToolbarToggle>("toggle");
			_toggle.RegisterValueChangedCallback(val => OnClicked?.Invoke(this, val.newValue));

			ui.Q<Label>("label").text = name;
			if (File.Exists(Combination.ThumbPath)) {
				var tex = asset.LoadThumbTexture(Combination.ThumbPath);
				ui.Q<Image>("thumbnail").image = tex;
			}
			Add(ui);
			// Captured pointer events go directly to the toggle, bypassing its ancestors.
			_toggle.RegisterCallback<PointerDownEvent>(evt => {
				_dragReady = evt.button == 0 && evt.isPrimary && asset.Object is GameObject;
				_dragStart = evt.position;
			}, TrickleDown.TrickleDown);
			_toggle.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
			_toggle.RegisterCallback<PointerUpEvent>(_ => _dragReady = false, TrickleDown.TrickleDown);
			_toggle.RegisterCallback<PointerCaptureOutEvent>(_ => _dragReady = false);
			RegisterCallback<DetachFromPanelEvent>(_ => _dragReady = false);
		}

		private void OnPointerMove(PointerMoveEvent evt)
		{
			if (!_dragReady || (evt.pressedButtons & 1) == 0 || (evt.position - _dragStart).sqrMagnitude < 36f) {
				return;
			}
			_dragReady = false;
			DragAndDrop.PrepareStartDrag();
			AssetBrowser.AttachMaterialCombination(Combination);
			_toggle.ReleasePointer(evt.pointerId);
			DragAndDrop.StartDrag($"{Combination.Asset.Object.name} ({Name})");
			evt.StopImmediatePropagation();
		}
	}
}
