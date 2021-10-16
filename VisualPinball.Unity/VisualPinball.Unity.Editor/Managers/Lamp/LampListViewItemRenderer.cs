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
using VisualPinball.Engine.Math;
using Color = UnityEngine.Color;

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
			Source = 2,
			Type = 3,
			Element = 4,
			Channel = 5,
			FadingSteps = 6,
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
					if (data.IsCoil) {
						RenderCoilId(lampStatuses, data, cellRect);
					} else {
						RenderId(lampStatuses, ref data.Id, id => data.Id = id, data, cellRect, updateAction);
					}
					break;
				case LampListColumn.Description:
					RenderDescription(data, cellRect, updateAction);
					break;
				case LampListColumn.Source:
					RenderSource(data, cellRect, updateAction);
					break;
				case LampListColumn.Type:
					RenderType(data, cellRect, updateAction);
					break;
				case LampListColumn.Element:
					RenderDevice(data, cellRect, updateAction);
					break;
				case LampListColumn.Channel:
					RenderChannel(data, cellRect, updateAction);
					break;
				case LampListColumn.FadingSteps:
					RenderFadingSteps(data, cellRect, updateAction);
					break;

			}
			EditorGUI.EndDisabledGroup();
		}

		private void RenderCoilId(Dictionary<string, float> lampStatuses, LampListData lampListData, Rect cellRect)
		{
			// add some padding
			cellRect.x = cellRect.width - 45;
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

		private void RenderSource(LampListData lampListData, Rect cellRect, Action<LampListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var source = (LampSource)EditorGUI.EnumPopup(cellRect, lampListData.Source);
			if (EditorGUI.EndChangeCheck()) {
				lampListData.Source = source;
				updateAction(lampListData);
			}
		}

		private void RenderChannel(LampListData lampListData, Rect cellRect, Action<LampListData> updateAction)
		{
			if (lampListData.Type != LampType.RgbMulti) {
				return;
			}
			EditorGUI.BeginChangeCheck();
			var channel = (ColorChannel)EditorGUI.EnumPopup(cellRect, lampListData.Channel);
			if (EditorGUI.EndChangeCheck()) {
				lampListData.Channel = channel;
				updateAction(lampListData);
			}
		}

		private void RenderFadingSteps(LampListData lampListData, Rect cellRect, Action<LampListData> updateAction)
		{
			if (lampListData.Type != LampType.SingleFading) {
				return;
			}
			EditorGUI.BeginChangeCheck();
			var value = EditorGUI.IntField(cellRect, lampListData.FadingSteps);
			if (EditorGUI.EndChangeCheck()) {
				lampListData.FadingSteps = value;
				updateAction(lampListData);
			}
		}

		protected override Texture GetIcon(LampListData lampListData)
		{
			return lampListData.Device != null
				? Icons.ByComponent(lampListData.Device, IconSize.Small)
				: null;
		}
	}
}
