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
using System.Collections.Generic;
using System.Linq;
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
			var materialCombinations = asset.GetCombinations(true, false, false)
				.Where(c => !c.IsOriginal)
				.ToArray();

			// material variations
			if (materialCombinations.Length > 0) {
				Clear();

				// invalid combinations resolve to the closest valid one, so several of them can end up being the same.
				var validCombinations = asset.GetCombinations(true, true).ToArray();
				var addedThumbIds = new HashSet<string>();
				foreach (var combination in materialCombinations) {
					var validCombination = combination.GetValidCombination(validCombinations);
					if (validCombination == null || !addedThumbIds.Add(validCombination.ThumbId)) {
						continue; // skip invalid combinations and duplicates
					}
					var combinationEl = new AssetMaterialCombinationElement(validCombination, asset, GetLabel(validCombination));
					combinationEl.OnClicked += OnVariationClicked;
					_container.Add(combinationEl);
				}

				SetVisibility(_foldout, true);

			} else {
				SelectedMaterialCombination = null;
				SetVisibility(_foldout, false);
			}
		}

		/// <summary>
		/// Names the combination by its material overrides. If they all share the same name, like a body and
		/// a cover that a rule keeps equal, that name alone is enough.
		/// </summary>
		private static string GetLabel(AssetMaterialCombination combination)
		{
			var overrides = combination.Overrides.Where(o => !o.IsDecal).ToArray();
			if (overrides.Length > 1 && overrides.All(o => o.Override.Name == overrides[0].Override.Name)) {
				return overrides[0].Override.Name;
			}
			return string.Join(", ", overrides.Select(o => $"{o.Override.Name} {o.Variation.Name}"));
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
