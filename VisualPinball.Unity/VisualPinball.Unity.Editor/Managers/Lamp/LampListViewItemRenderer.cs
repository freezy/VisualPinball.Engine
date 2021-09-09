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
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity.Editor
{
	public class LampListViewItemRenderer : ListViewItemRenderer<LampListData, GamelogicEngineLamp, float>
	{
		protected override List<GamelogicEngineLamp> GleItems => _gleLamps;
		protected override GamelogicEngineLamp InstantiateGleItem(string id) => new GamelogicEngineLamp(id);
		protected override Texture2D StatusIcon(float status) => Icons.Light(IconSize.Small, status > 0 ? IconColor.Orange : IconColor.Gray);

		private enum LampListColumn
		{
			Id = 0,
			Description = 1,
			Element = 2,
			Type = 3,
			Color = 4,
		}

		private readonly List<GamelogicEngineLamp> _gleLamps;

		private readonly ObjectReferencePicker<ILampDeviceComponent> _devicePicker;

		public LampListViewItemRenderer(List<GamelogicEngineLamp> gleLamps, TableComponent tableComponent)
		{
			_gleLamps = gleLamps;
			_devicePicker = new ObjectReferencePicker<ILampDeviceComponent>("Lamps", tableComponent, false);
		}

		public void Render(TableComponent tableComponent, LampListData data, Rect cellRect, int column, Action<LampListData> updateAction)
		{
			EditorGUI.BeginDisabledGroup(Application.isPlaying);
			var lampStatuses = Application.isPlaying
				? tableComponent.gameObject.GetComponent<Player>()?.LampStatuses
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
					RenderDevice(data, cellRect, updateAction);
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

		protected override void RenderDeviceElement(LampListData listData, Rect cellRect, Action<LampListData> updateAction)
		{
			_devicePicker.Render(cellRect, listData.Device, item => {
				listData.Device = item;
				updateAction(listData);
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

		private void RenderRgb(Dictionary<string, float> lampStatuses, LampListData data, Rect cellRect, Action<LampListData> updateAction)
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

		protected override Texture GetIcon(LampListData lampListData)
		{
			return lampListData.Device != null
				? Icons.ByComponent(lampListData.Device, IconSize.Small)
				: null;
		}
	}
}
