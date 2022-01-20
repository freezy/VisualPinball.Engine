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
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public class LabelsThumbnailView : ThumbnailView<LabelThumbnailElement>
	{
		GUIStyle _labelButtonStyle;

		public LabelsHandler LabelsHandler = null;

		public bool Editable = false;

		public LabelsThumbnailView(IEnumerable<LabelThumbnailElement> data) : base(data) 
		{
			ShowToolbar = false;
		}

		protected override void InitCommonStyles()
		{
			if (!_commonStyles.Inited) {
				_commonStyles.DefaultStyle = new GUIStyle(GUI.skin.FindStyle("AssetLabel"));
				_commonStyles.SelectedStyle = new GUIStyle(_commonStyles.DefaultStyle);
				_commonStyles.SelectedStyle.active.textColor = Color.white;
				_commonStyles.SelectedStyle.normal = _commonStyles.SelectedStyle.active;
				_commonStyles.SelectedStyle.hover = _commonStyles.SelectedStyle.active;
				_commonStyles.SelectedStyle.focused = _commonStyles.SelectedStyle.active;
				_commonStyles.NameStyle = new GUIStyle("label");
				_commonStyles.HoverStyle = new GUIStyle();
				_commonStyles.HoverStyle.normal.background = TextureExtensions.CreatePixelTexture(new Color(.25f, .25f, .25f));

				_labelButtonStyle = new GUIStyle(GUI.skin.FindStyle("AssetLabel Icon"));
			}
		}

		protected override void OnGUIEnd(Rect r)
		{
			if (Editable && !HasData) {
				var popupRect = EditorGUILayout.GetControlRect(false);
				if (EditorGUI.DropdownButton(popupRect, new GUIContent(), FocusType.Passive, _labelButtonStyle)) {
					if (LabelsHandler != null) {
						var labelsList = new PopupLabelList.InputData() { m_AllowCustom = true, m_CloseOnSelection = false, m_EnableAutoCompletion = true, m_SortAlphabetically = true, m_OnSelectCallback = OnSelectLabelCallback };
						labelsList.m_ListElements = LabelsHandler.GetLabels().Select(L => new PopupListElement(L.FullLabel)).ToList();
						PopupWindow.Show(popupRect, new PopupLabelList(labelsList));
					}
				}
			}
		}

		private void OnSelectLabelCallback(PopupListElement element)
		{
			element.selected = !element.selected;
		}
	}
}
