// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	public class LabelsHandler
	{
		public enum LabelType
		{
			LabelLibraries,
			Assets,
			All
		}

		private Dictionary<LabelType, PinballLabelList> _labels = new Dictionary<LabelType, PinballLabelList>() {
			{LabelType.LabelLibraries, new PinballLabelList() },
			{LabelType.Assets, new PinballLabelList() }
		};

		private Dictionary<LabelType, PinballLabelCategoryList> _categories = new Dictionary<LabelType, PinballLabelCategoryList>() {
			{LabelType.LabelLibraries, new PinballLabelCategoryList() },
			{LabelType.Assets, new PinballLabelCategoryList() }
		};

		public LabelsHandler() 
		{
		}

		private void BuildFromLibraries()
		{
			ClearAssetLabels();

			var guids = AssetDatabase.FindAssets("t: LabelsLibraryAsset");
			foreach (var guid in guids) {
				var asset = AssetDatabase.LoadAssetAtPath<LabelsLibraryAsset>(AssetDatabase.GUIDToAssetPath(guid));
				foreach (var category in asset.Categories) {
					if (!string.IsNullOrEmpty(category.Name)) {
						_categories[LabelType.LabelLibraries].Add(new PinballLabelCategory(category) {
							Name = category.Name.Replace(PinballLabel.Separator, PinballLabelCategory.Separator),
						}) ;
					}
				}
				foreach(var label in asset.Labels) {
					if (!string.IsNullOrEmpty(label.FullLabel)) {
						_labels[LabelType.LabelLibraries].Add(new PinballLabel(label.FullLabel));
					}
				}
			}
		}

		public void Init()
		{
			BuildFromLibraries();
		}

		public void ClearAssetLabels()
		{
			_categories[LabelType.Assets].Clear();
			_labels[LabelType.Assets].Clear();
		}

		public void AddLabels(string[] labels)
		{
			foreach(var label in labels) {
				PinballLabel pLabel = new PinballLabel(label);
				if (!string.IsNullOrEmpty(pLabel.FullLabel)) {
					if (!_labels[LabelType.LabelLibraries].Contains(pLabel)) {
						if (_labels[LabelType.Assets].Add(pLabel) && !string.IsNullOrEmpty(pLabel.Category)) {
							var category = new PinballLabelCategory() { Name = pLabel.Category };
							if (!_categories[LabelType.LabelLibraries].Contains(category)) {
								_categories[LabelType.Assets].Add(category);
							}
						}
					}
				}
			}
		}

		public List<PinballLabel> GetLabels(LabelType type = LabelType.All)
		{
			if (type == LabelType.All) {
				var list = new List<PinballLabel>();
				foreach(var labels in _labels) {
					list.AddRange(labels.Value);
				}
				return list;
			} else {
				return _labels[type].ToList();
			}
		}
	}
}
