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

// ReSharper disable InconsistentNaming

using UnityEditor;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(Asset))]
	public class AssetInspector : UnityEditor.Editor
	{
		private Asset _asset;

		public VisualTreeAsset InspectorXML;
		public StyleSheet InspectorStyleSheet;

		private void OnEnable()
		{
			_asset = target as Asset;
		}

		public override VisualElement CreateInspectorGUI()
		{
			var ui = new VisualElement();
			InspectorXML.CloneTree(ui);
			ui.styleSheets.Add(InspectorStyleSheet);

			ui.Q<Label>("title").text = _asset.Object.name;
			ui.Q<Image>("library-icon").image = Icons.AssetLibrary(IconSize.Small);
			ui.Q<Label>("library-name").text = _asset.Library.Name;

			ui.Q<Image>("category-icon").image = EditorGUIUtility.IconContent("d_Folder Icon").image;
			ui.Q<Label>("category-name").text = _asset.Category?.Name ?? "<no category>";
			ui.Q<Image>("date-icon").image = Icons.Calendar(IconSize.Small);
			ui.Q<Label>("date-value").text = _asset.AddedAt.ToLongDateString();

			ui.Q<PreviewEditorElement>("preview").Object = _asset.Object;

			return ui;
		}
	}
}
