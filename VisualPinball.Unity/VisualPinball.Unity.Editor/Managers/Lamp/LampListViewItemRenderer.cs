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

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity.Editor
{
	public class LampListViewItemRenderer
	{
		private readonly string[] OPTIONS_LAMP_TYPE = { "Single On|Off", "Single Fading", "RGB Multi", "RGB" };

		private enum LampListColumn
		{
			Id = 0,
			Description = 1,
			Element = 2,
			Type = 3,
			Color = 4,
		}

		private readonly List<GamelogicEngineLamp> _gleLamps;

		private readonly ObjectReferencePicker<ILampDeviceAuthoring> _devicePicker;

		public LampListViewItemRenderer(List<GamelogicEngineLamp> gleLamps, TableAuthoring tableComponent)
		{
			_gleLamps = gleLamps;
			_devicePicker = new ObjectReferencePicker<ILampDeviceAuthoring>("Lamps", tableComponent, false);
		}

		public void Render(TableAuthoring tableAuthoring, LampListData data, Rect cellRect, int column, Action<LampListData> updateAction)
		{
			EditorGUI.BeginDisabledGroup(Application.isPlaying);
			var lampStatuses = Application.isPlaying
				? tableAuthoring.gameObject.GetComponent<Player>()?.LampStatuses
				: null;

			switch ((LampListColumn)column) {
				case LampListColumn.Id:
					if (data.Source == LampSource.Coils) {
						RenderCoilId(lampStatuses, data, cellRect);
					} else {
						RenderId(lampStatuses, ref data.Id, id => data.Id = id, data, cellRect, updateAction);
					}
					break;
				case LampListColumn.Description:
					RenderDescription(data, cellRect, updateAction);
					break;
				case LampListColumn.Element:
					RenderElement(tableAuthoring, data, cellRect, updateAction);
					break;
				case LampListColumn.Type:
					RenderType(data, cellRect, updateAction);
					break;
				case LampListColumn.Color:
					switch (data.Type) {
						case LampType.RgbMulti:
							RenderRgb(lampStatuses, data, cellRect, updateAction);
							break;
					}
					break;
			}
			EditorGUI.EndDisabledGroup();
		}

		private void RenderCoilId(Dictionary<string, float> lampStatuses, LampListData lampListData, Rect cellRect)
		{
			// add some padding
			cellRect.x += 2;
			cellRect.width -= 4;


			var statusAvail = Application.isPlaying && lampStatuses != null && lampStatuses.ContainsKey(lampListData.Id);
			var icon = Icons.Coil(IconSize.Small, statusAvail && lampStatuses[lampListData.Id] > 0 ? IconColor.Orange : IconColor.Gray);
			if (icon != null) {
				var iconRect = cellRect;
				iconRect.width = 20;
				var guiColor = GUI.color;
				GUI.color = Color.clear;
				EditorGUI.DrawTextureTransparent(iconRect, icon, ScaleMode.ScaleToFit);
				GUI.color = guiColor;
			}
			cellRect.x += 20;
			cellRect.width -= 20;

			EditorGUI.LabelField(cellRect, lampListData.Id);
		}

		private void RenderId(IReadOnlyDictionary<string, float> lampStatuses, ref string id, Action<string> setId, LampListData lampListData, Rect cellRect, Action<LampListData> updateAction)
		{
			// add some padding
			cellRect.x += 2;
			cellRect.width -= 4;

			var options = new List<string>(_gleLamps.Select(entry => entry.Id).ToArray());

			if (options.Count > 0) {
				options.Add("");
			}
			options.Add("Add...");

			if (Application.isPlaying && lampStatuses != null) {
				var iconRect = cellRect;
				iconRect.width = 20;
				cellRect.x += 25;
				cellRect.width -= 25;
				if (lampStatuses.ContainsKey(id)) {
					var lampStatus = lampStatuses[id];
					var icon = Icons.Light(IconSize.Small, lampStatus > 0 ? IconColor.Orange : IconColor.Gray);
					var guiColor = GUI.color;
					GUI.color = Color.clear;
					EditorGUI.DrawTextureTransparent(iconRect, icon, ScaleMode.ScaleToFit);
					GUI.color = guiColor;
				}
			}

			EditorGUI.BeginChangeCheck();
			var index = EditorGUI.Popup(cellRect, options.IndexOf(id), options.ToArray());
			if (EditorGUI.EndChangeCheck()) {
				if (index == options.Count - 1) {
					PopupWindow.Show(cellRect, new ManagerListTextFieldPopup("ID", "", newId => {
						if (!_gleLamps.Exists(entry => entry.Id == newId)) {
							_gleLamps.Add(new GamelogicEngineLamp(newId));
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

			RenderPlayfieldElement(tableAuthoring, lampListData, cellRect, updateAction);
		}

		private void RenderPlayfieldElement(TableAuthoring tableAuthoring, LampListData lampListData, Rect cellRect, Action<LampListData> updateAction)
		{
			_devicePicker.Render(cellRect, lampListData.Device, item => {
				lampListData.Device = item;
				updateAction(lampListData);
			});
		}

		private void RenderType(LampListData lampListData, Rect cellRect, Action<LampListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var type = (LampType)EditorGUI.EnumPopup(cellRect, lampListData.Type);
			if (EditorGUI.EndChangeCheck()) {
				lampListData.Type = type;
				updateAction(lampListData);
			}
		}

		private void RenderRgb(IReadOnlyDictionary<string, float> lampStatuses, LampListData data, Rect cellRect, Action<LampListData> updateAction)
		{
			var pad = 2;
			var width = cellRect.width / 3;
			var c = cellRect;
			c.width = width - pad;
			RenderId(lampStatuses, ref data.Id, id => data.Id = id, data, c, updateAction);
			c.x += width + pad;
			RenderId(lampStatuses, ref data.Green, id => data.Green = id, data, c, updateAction);
			c.x += width + pad;
			RenderId(lampStatuses, ref data.Blue, id => data.Blue = id, data, c, updateAction);
		}

		private Texture GetIcon(LampListData lampListData)
		{
			Texture2D icon = null;

			if (lampListData.Device != null) {
				icon = Icons.ByComponent(lampListData.Device, size: IconSize.Small);
			}

			return icon;
		}
	}
}
