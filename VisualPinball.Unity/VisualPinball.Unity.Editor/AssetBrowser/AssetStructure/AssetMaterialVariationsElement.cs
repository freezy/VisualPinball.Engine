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
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// The element that makes radio buttons out of the <see cref="AssetMaterialCombinationElement"/>s and
	/// exposes which is selected.
	/// </summary>
	public class AssetMaterialVariationsElement : VisualElement
	{
		#region Uxml

		public new class UxmlFactory : UxmlFactory<AssetMaterialVariationsElement, UxmlTraits> { }

		public new class UxmlTraits : VisualElement.UxmlTraits
		{
			private readonly UxmlStringAttributeDescription _text = new() { name = "text" };
			private readonly UxmlStringAttributeDescription _tooltip = new() { name = "tooltip" };

			public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
			{
				get { yield break; }
			}

			public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
			{
				base.Init(ve, bag, cc);
				var ate = ve as AssetMaterialVariationsElement;

				ate!.Text = _text.GetValueFromBag(bag, cc);
				ate!.Tooltip = _tooltip.GetValueFromBag(bag, cc);
			}
		}

		#endregion

		public AssetMaterialCombinationElement SelectedMaterialCombination { get; private set; }

		public event EventHandler<AssetMaterialCombinationElement> OnSelected;

		private readonly Foldout _foldout;
		private readonly ScrollView _container;

		private string Text { set => _foldout.text = value; }
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
			// material variations
			if (asset.MaterialVariations?.Count > 0) {
				Clear();

				foreach (var combination in AssetMaterialCombination.GetCombinations(asset).Where(c => !c.IsOriginal)) {
					var combinationEl = new AssetMaterialCombinationElement(combination);
					combinationEl.OnClicked += OnVariationClicked;
					_container.Add(combinationEl);
				}

				SetVisibility(_foldout, true);

			} else {
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
