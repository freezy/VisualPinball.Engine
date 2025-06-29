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
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// The element that makes radio buttons out of the <see cref="AssetMaterialCombinationElement"/>s and
	/// exposes which is selected.
	/// </summary>
	[UxmlElement]
	public partial class AssetDecalVariationsElement : VisualElement
	{
		public AssetMaterialCombinationElement SelectedMaterialCombination { get; private set; }

		public event EventHandler<AssetMaterialCombinationElement> OnSelected;

		private readonly Foldout _foldout;

		[UxmlAttribute("text")]
		private string Text { set => _foldout.text = value; }

		[UxmlAttribute("tooltip")]
		private string Tooltip { set => _foldout.tooltip = value; }

		public AssetDecalVariationsElement()
		{
			_foldout = new Foldout();
			Add(_foldout);
		}

		public void SetValue(Asset asset, AssetMaterialCombination materialCombination)
		{
			if (asset.DecalVariations == null || asset.DecalVariations.Count == 0) {
				SetVisibility(_foldout, false);
				SelectedMaterialCombination = null;
				return;
			}
			Clear();

			switch (asset.GroupBy.GroupBy) {
				case AssetVariationGroupBy.Decal:
					Render(GroupByDecal(asset, materialCombination), asset);
					break;
				case AssetVariationGroupBy.Object:
					Render(GroupByObject(asset, materialCombination, asset.GroupBy.Object), asset);
					break;
			}
			SetVisibility(_foldout, true);
		}

		private static IEnumerable<IGrouping<string, AssetMaterialCombination>> GroupByDecal(Asset asset, AssetMaterialCombination materialCombination)
		{
			return asset.CombineWith(materialCombination)
				.Where(mc => !mc.EqualsOverrides(materialCombination))
				.GroupBy(x => x.DecalVariationNames);
		}

		private static IEnumerable<IGrouping<AssetMaterialVariationOverride, AssetMaterialCombination>> GroupByObject(Asset asset, AssetMaterialCombination materialCombination, Object obj)
		{
			return asset.CombineWith(materialCombination)
				.GroupBy(dv => dv.Overrides?.FirstOrDefault(o => o.Variation?.Target?.Object == obj));
		}

		private void Render<T>(IEnumerable<IGrouping<T, AssetMaterialCombination>> combinations, Asset asset)
		{
			foreach (var group in combinations) {
				var container = new ScrollView { mode = ScrollViewMode.Horizontal };
				_foldout.Add(container);

				foreach (var combination in group) {

					var combinationEl = new AssetMaterialCombinationElement(combination, asset, true);
					combinationEl.OnClicked += OnVariationClicked;
					container.Add(combinationEl);
				}
			}
		}

		private void OnVariationClicked(object clickedVariation, bool enabled)
		{
			if (enabled) {
				foreach (var container in _foldout.Children()) {
					foreach (var variation in container.Children().Select(c => c as AssetMaterialCombinationElement)) {
						if (clickedVariation != variation) {
							variation!.Enabled = false;
						}
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
			foreach (var container in _foldout.Children()) {
				foreach (var child in container.Children()) {
					(child as AssetMaterialCombinationElement)!.OnClicked -= OnVariationClicked;
				}
				container.Clear();
			}
			_foldout.Clear();
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
