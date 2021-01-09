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

using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using VisualPinball.Engine.VPT;
using System.Linq;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity.Editor
{
	public class LampListViewItemRenderer
	{
		private readonly string[] OPTIONS_LAMP_DESTINATION = { "Playfield" };

		private enum LampListColumn
		{
			Id = 0,
			Description = 1,
			Destination = 2,
			Element = 3,
		}

		private readonly List<GamelogicEngineLamp> _gleLamps;
		private readonly Dictionary<string, ILampAuthoring> _lamps;

		private AdvancedDropdownState _itemPickDropdownState;

		public LampListViewItemRenderer(List<GamelogicEngineLamp> gleLamps, Dictionary<string, ILampAuthoring> lamps)
		{
			_gleLamps = gleLamps;
			_lamps = lamps;
		}

		public void Render(TableAuthoring tableAuthoring, LampListData data, Rect cellRect, int column, Action<LampListData> updateAction)
		{
			switch ((LampListColumn)column) {
				case LampListColumn.Id:
					RenderId(ref data.Id, id => data.Id = id, data, cellRect, updateAction);
					break;
				case LampListColumn.Description:
					RenderDescription(data, cellRect, updateAction);
					break;
				case LampListColumn.Destination:
					RenderDestination(data, cellRect, updateAction);
					break;
				case LampListColumn.Element:
					RenderElement(tableAuthoring, data, cellRect, updateAction);
					break;
			}
		}

		private void RenderId(ref string id, Action<string> setId, LampListData lampListData, Rect cellRect, Action<LampListData> updateAction)
		{
			// add some padding
			cellRect.x += 2;
			cellRect.width -= 4;

			var options = new List<string>(_gleLamps.Select(entry => entry.Id).ToArray());

			if (options.Count > 0) {
				options.Add("");
			}

			options.Add("Add...");

			EditorGUI.BeginChangeCheck();
			var index = EditorGUI.Popup(cellRect, options.IndexOf(id), options.ToArray());
			if (EditorGUI.EndChangeCheck()) {
				if (index == options.Count - 1) {
					PopupWindow.Show(cellRect, new ManagerListTextFieldPopup("ID", "", newId => {
						if (_gleLamps.Exists(entry => entry.Id == newId)) {
							_gleLamps.Add(new GamelogicEngineLamp
							{
								Id = newId
							});
						}

						setId(newId);
						updateAction(lampListData);
					}));

				}
				else {
					setId(_gleLamps[index].Id);
					updateAction(lampListData);
				}
			}
		}

		private void RenderDescription(LampListData lampListData, Rect cellRect, Action<LampListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var value = EditorGUI.TextField(cellRect, lampListData.Description);
			if (EditorGUI.EndChangeCheck()) {
				lampListData.Description = value;
				updateAction(lampListData);
			}
		}

		private void RenderDestination(LampListData lampListData, Rect cellRect, Action<LampListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var index = EditorGUI.Popup(cellRect, lampListData.Destination, OPTIONS_LAMP_DESTINATION);
			if (EditorGUI.EndChangeCheck()) {
				if (lampListData.Destination != index) {
					lampListData.Destination = index;
					updateAction(lampListData);
				}
			}
		}

		private void RenderElement(TableAuthoring tableAuthoring, LampListData lampListData, Rect cellRect, Action<LampListData> updateAction)
		{
			var icon = GetIcon(lampListData);

			if (icon != null) {
				var iconRect = cellRect;
				iconRect.width = 20;
				var guiColor = GUI.color;
				GUI.color = Color.clear;
				EditorGUI.DrawTextureTransparent(iconRect, icon, ScaleMode.ScaleToFit);
				GUI.color = guiColor;
			}

			cellRect.x += 25;
			cellRect.width -= 25;

			switch (lampListData.Destination) {
				case LampDestination.Playfield:
					RenderPlayfieldElement(tableAuthoring, lampListData, cellRect, updateAction);
					break;
			}
		}

		private void RenderPlayfieldElement(TableAuthoring tableAuthoring, LampListData lampListData, Rect cellRect, Action<LampListData> updateAction)
		{
			if (GUI.Button(cellRect, lampListData.PlayfieldItem, EditorStyles.objectField) || GUI.Button(cellRect, "", GUI.skin.GetStyle("IN ObjectField"))) {
				if (_itemPickDropdownState == null) {
					_itemPickDropdownState = new AdvancedDropdownState();
				}

				var dropdown = new ItemSearchableDropdown<ILampAuthoring>(
					_itemPickDropdownState,
					tableAuthoring,
					"Lamp Items",
					item => {
						lampListData.PlayfieldItem = item.Name;
						updateAction(lampListData);
					}
				);
				dropdown.Show(cellRect);
			}
		}

		private UnityEngine.Texture GetIcon(LampListData lampListData)
		{
			Texture2D icon = null;

			switch (lampListData.Destination) {
				case LampDestination.Playfield: {
						if (_lamps.ContainsKey(lampListData.PlayfieldItem.ToLower())) {
							icon = Icons.ByComponent(_lamps[lampListData.PlayfieldItem.ToLower()], size: IconSize.Small);
						}
						break;
					}
			}

			return icon;
		}
	}
}
