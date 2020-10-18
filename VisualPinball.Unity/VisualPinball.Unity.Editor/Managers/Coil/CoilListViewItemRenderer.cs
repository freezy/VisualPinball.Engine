// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	public class CoilListViewItemRenderer
	{
		private readonly string[] OPTIONS_COIL_DESTINATION = { "Playfield" };
		private readonly string[] OPTIONS_COIL_TYPE = { "On \u2215 Off", "Dual-Wound" };

		private enum CoilListColumn
		{
			Id = 0,
			Description = 1,
			Destination = 2,
			Element = 3,
			Type = 4,
			HoldCoilId = 5,
		}

		private readonly List<string> _ids;
		private readonly Dictionary<string, ICoilAuthoring> _coils;

		private AdvancedDropdownState _itemPickDropdownState;

		public CoilListViewItemRenderer(List<string> ids, Dictionary<string, ICoilAuthoring> coils)
		{
			_ids = ids;
			_coils = coils;
		}

		public void Render(TableAuthoring tableAuthoring, CoilListData data, Rect cellRect, int column, Action<CoilListData> updateAction)
		{
			switch ((CoilListColumn)column)
			{
				case CoilListColumn.Id:
					RenderId(ref data.Id, id => data.Id = id, data, cellRect, updateAction);
					break;
				case CoilListColumn.Description:
					RenderDescription(data, cellRect, updateAction);
					break;
				case CoilListColumn.Destination:
					RenderDestination(data, cellRect, updateAction);
					break;
				case CoilListColumn.Element:
					RenderElement(tableAuthoring, data, cellRect, updateAction);
					break;
				case CoilListColumn.Type:
					RenderType(data, cellRect, updateAction);
					break;
				case CoilListColumn.HoldCoilId:
					if (data.Type == CoilType.DualWound) {
						RenderId(ref data.HoldCoilId, id => data.HoldCoilId = id, data, cellRect, updateAction);
					}
					break;
			}
		}

		private void RenderId(ref string id, Action<string> setId, CoilListData coilListData, Rect cellRect, Action<CoilListData> updateAction)
		{
			// add some padding
			cellRect.x += 2;
			cellRect.width -= 4;

			var options = new List<string>(_ids);

			if (options.Count > 0) {
				options.Add("");
			}

			options.Add("Add...");

			EditorGUI.BeginChangeCheck();
			var index = EditorGUI.Popup(cellRect, options.IndexOf(id), options.ToArray());
			if (EditorGUI.EndChangeCheck()) {
				if (index == options.Count - 1) {
					PopupWindow.Show(cellRect, new ManagerListTextFieldPopup("ID", "", newId => {
						if (_ids.IndexOf(newId) == -1) {
							_ids.Add(newId);
						}

						setId(newId);
						updateAction(coilListData);
					}));

				} else {
					setId(_ids[index]);
					updateAction(coilListData);
				}
			}
		}

		private void RenderDescription(CoilListData coilListData, Rect cellRect, Action<CoilListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var value = EditorGUI.TextField(cellRect, coilListData.Description);
			if (EditorGUI.EndChangeCheck())
			{
				coilListData.Description = value;
				updateAction(coilListData);
			}
		}

		private void RenderDestination(CoilListData coilListData, Rect cellRect, Action<CoilListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var index = EditorGUI.Popup(cellRect, coilListData.Destination, OPTIONS_COIL_DESTINATION);
			if (EditorGUI.EndChangeCheck())
			{
				if (coilListData.Destination != index)
				{
					coilListData.Destination = index;
					updateAction(coilListData);
				}
			}
		}

		private void RenderElement(TableAuthoring tableAuthoring, CoilListData coilListData, Rect cellRect, Action<CoilListData> updateAction)
		{
			var icon = GetIcon(coilListData);

			if (icon != null)
			{
				var iconRect = cellRect;
				iconRect.width = 20;
				var guiColor = GUI.color;
				GUI.color = Color.clear;
				EditorGUI.DrawTextureTransparent(iconRect, icon, ScaleMode.ScaleToFit);
				GUI.color = guiColor;
			}

			cellRect.x += 25;
			cellRect.width -= 25;

			switch (coilListData.Destination)
			{
				case CoilDestination.Playfield:
					RenderPlayfieldElement(tableAuthoring, coilListData, cellRect, updateAction);
					break;
			}
		}

		private void RenderPlayfieldElement(TableAuthoring tableAuthoring, CoilListData coilListData, Rect cellRect, Action<CoilListData> updateAction)
		{
			if (GUI.Button(cellRect, coilListData.PlayfieldItem, EditorStyles.objectField) || GUI.Button(cellRect, "", GUI.skin.GetStyle("IN ObjectField")))
			{
				if (_itemPickDropdownState == null)
				{
					_itemPickDropdownState = new AdvancedDropdownState();
				}

				var dropdown = new ItemSearchableDropdown<ICoilAuthoring>(
					_itemPickDropdownState,
					tableAuthoring,
					"Coil Items",
					item => {
						coilListData.PlayfieldItem = item.Name;
						updateAction(coilListData);
					}
				);
				dropdown.Show(cellRect);
			}
		}

		private void RenderType(CoilListData coilListData, Rect cellRect, Action<CoilListData> updateAction)
		{
			if (coilListData.Destination == CoilDestination.Playfield)
			{
				EditorGUI.BeginChangeCheck();
				var index = EditorGUI.Popup(cellRect, (int)coilListData.Type, OPTIONS_COIL_TYPE);
				if (EditorGUI.EndChangeCheck())
				{
					coilListData.Type = index;
					updateAction(coilListData);
				}
			}
		}

		private UnityEngine.Texture GetIcon(CoilListData coilListData)
		{
			Texture2D icon = null;

			switch (coilListData.Destination)
			{
				case CoilDestination.Playfield:
					{
						if (_coils.ContainsKey(coilListData.PlayfieldItem.ToLower()))
						{
							icon = Icons.ByComponent(_coils[coilListData.PlayfieldItem.ToLower()], size: IconSize.Small);
						}
						break;
					}
			}

			return icon;
		}
	}
}
