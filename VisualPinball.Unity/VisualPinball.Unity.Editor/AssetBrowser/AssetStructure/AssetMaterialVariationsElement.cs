﻿// Visual Pinball Engine
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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// The element that makes radio buttons out of the <see cref="AssetMaterialCombinationElement"/>s and
	/// exposes which is selected.
	/// </summary>
	[UxmlElement]
	public partial class AssetMaterialVariationsElement : VisualElement
	{
		public AssetMaterialCombinationElement SelectedMaterialCombination { get; private set; }

		public event EventHandler<AssetMaterialCombinationElement> OnSelected;

		private readonly Foldout _foldout;
		private readonly ScrollView _container;

		[UxmlAttribute("text")]
		private string Text { set => _foldout.text = value; }

		[UxmlAttribute("tooltip")]
		private string Tooltip { set => _foldout.tooltip = value; }

		public AssetMaterialVariationsElement()
		{
			_foldout = new Foldout();
			_container = new ScrollView { mode = ScrollViewMode.Horizontal };

			_foldout.Add(_container);
			Add(_foldout);
		}

		public void SetValue(Asset asset)
		{

			var materialCombinations = AssetMaterialCombination.GetCombinations(asset)
				.Where(c => !c.IsOriginal)
				.ToArray();

			// material variations
			if (materialCombinations.Length > 0) {
				Clear();

				foreach (var combination in materialCombinations) {
					var combinationEl = new AssetMaterialCombinationElement(combination, asset);
					combinationEl.OnClicked += OnVariationClicked;
					_container.Add(combinationEl);
				}

				SetVisibility(_foldout, true);

			} else {
				SelectedMaterialCombination = null;
				SetVisibility(_foldout, false);
			}
		}

		private void OnVariationClicked(object clickedVariation, bool enabled)
		{
			if (enabled) {
				foreach (var variation in _container.Children().Select(c => c as AssetMaterialCombinationElement)) {
					if (clickedVariation != variation) {
						variation!.Enabled = false;
					}
				}
				SelectedMaterialCombination = clickedVariation as AssetMaterialCombinationElement;
				OnSelected?.Invoke(this, SelectedMaterialCombination);

			} else {
				SelectedMaterialCombination = null;
				OnSelected?.Invoke(this, null);
			}
		}

		private new void Clear()
		{
			foreach (var child in _container.Children()) {
				(child as AssetMaterialCombinationElement)!.OnClicked -= OnVariationClicked;
			}
			_container.Clear();
		}

		private static void SetVisibility(VisualElement element, bool isVisible)
		{
			switch (isVisible) {
				case false when !element.ClassListContains("hidden"):
					element.AddToClassList("hidden");
					break;
				case true when element.ClassListContains("hidden"):
					element.RemoveFromClassList("hidden");
					break;
			}
		}
	}
}
