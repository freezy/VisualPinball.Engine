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

using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	[CustomPropertyDrawer(typeof(AssetLink))]
	public class AssetLinkPropertyDrawer : PropertyDrawer
	{
		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			var ui = new VisualElement();
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetLinkPropertyDrawer.uxml");
			visualTree.CloneTree(ui);

			if (property.serializedObject.targetObject is Asset asset && asset.Library != null) {
				var nameField = ui.Q<SuggestingTextField>("name-field");
				nameField.SuggestOptions = asset.Library.GetLinkNames().ToArray();

				ui.Q<TextField>("url-field").RegisterCallback<KeyDownEvent>(evt => OnKeyPressed(evt, asset, nameField.Value));
			}

			return ui;
		}
		private static void OnKeyPressed(KeyDownEvent evt, Asset asset, string linkName)
		{
			if (evt.target is TextField linkField) {
				var partNumber = FirstPartNumber(asset);
				if (evt.ctrlKey && evt.keyCode == KeyCode.Space && partNumber != null && string.IsNullOrEmpty(linkField.value)) {
					linkField.value = linkName switch {
						"McMaster-Carr" => $"https://www.mcmaster.com/{partNumber}/",
						"Marco Specialties" => $"https://www.marcospecialties.com/pinball-parts/{partNumber}",
						_ => linkField.value
					};
				}
			}
		}

		private static string FirstPartNumber(Asset asset)
		{
			var attr = asset.Attributes.FirstOrDefault(a => a.Key == "Part Number");
			if (attr == null) {
				return null;
			}
			var partNumber = attr.Value.Split(",").FirstOrDefault();
			return string.IsNullOrEmpty(partNumber) ? null : partNumber.Trim();
		}
	}
}
