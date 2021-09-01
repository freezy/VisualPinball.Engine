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
	public abstract class ListViewItemRenderer<TListData, T> where TListData : IDeviceListData<T> where T : IGamelogicEngineDeviceItem
	{
		protected abstract List<T> GleItems { get; }

		protected abstract T InstantiateGleItem(string id);

		protected abstract Texture2D StatusIcon(bool status);

		protected void RenderId(Dictionary<string, bool> statuses, ref string id, Action<string> setId, TListData listData, Rect cellRect, Action<TListData> updateAction)
		{
			const float idWidth = 25f;
			const float padding = 2f;

			// add some padding
			cellRect.x += padding;
			cellRect.width -= 2 * padding;

			var dropdownRect = cellRect;
			dropdownRect.width -= idWidth + 2 * padding;

			var idRect = cellRect;
			idRect.width = idWidth;
			idRect.x += cellRect.width - idWidth;

			var options = new List<string>(GleItems.Select(entry => entry.Id).ToArray());
			if (options.Count > 0) {
				options.Add("");
			}
			options.Add("Add...");

			if (Application.isPlaying && statuses != null) {
				var iconRect = cellRect;
				iconRect.width = 20;
				dropdownRect.x += 25;
				dropdownRect.width -= 25;
				if (statuses.ContainsKey(id)) {
					var status = statuses[id];
					var icon = StatusIcon(status);
					var guiColor = GUI.color;
					GUI.color = Color.clear;
					EditorGUI.DrawTextureTransparent(iconRect, icon, ScaleMode.ScaleToFit);
					GUI.color = guiColor;
				}
			}

			EditorGUI.BeginChangeCheck();
			var index = EditorGUI.Popup(dropdownRect, options.IndexOf(id), options.ToArray());
			if (EditorGUI.EndChangeCheck()) {
				if (index == options.Count - 1) {
					PopupWindow.Show(dropdownRect, new ManagerListTextFieldPopup("ID", "", newId => {
						if (!GleItems.Exists(entry => entry.Id == newId)) {
							GleItems.Add(InstantiateGleItem(newId));
						}

						setId(newId);
						updateAction(listData);
					}));

				} else {
					setId(GleItems[index].Id);
					updateAction(listData);
				}
			}

			EditorGUI.BeginChangeCheck();
			var value = EditorGUI.IntField(idRect, listData.InternalId);
			if (EditorGUI.EndChangeCheck()) {
				listData.InternalId = value;
				updateAction(listData);
			}
		}

		protected void RenderDescription(TListData listData, Rect cellRect, Action<TListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var value = EditorGUI.TextField(cellRect, listData.Description);
			if (EditorGUI.EndChangeCheck())  {
				listData.Description = value;
				updateAction(listData);
			}
		}

		protected void RenderDeviceItemElement(TListData listData, Rect cellRect, Action<TListData> updateAction)
		{
			UpdateDeviceItem(listData);

			var onlyOneDeviceItem = listData.DeviceComponent != null && listData.DeviceComponent.AvailableDeviceItems.Count() == 1;
			if (onlyOneDeviceItem && string.IsNullOrEmpty(listData.DeviceComponent.AvailableDeviceItems.First().Description)) {
				return;
			}
			EditorGUI.BeginDisabledGroup(listData.DeviceComponent == null || onlyOneDeviceItem);

			var currentIndex = 0;
			var labels = Array.Empty<string>();
			IDeviceAuthoring<T> device = null;
			if (listData.DeviceComponent != null) {
				device = listData.DeviceComponent;
				labels = device.AvailableDeviceItems.Select(s => s.Description).ToArray();
				currentIndex = device.AvailableDeviceItems.TakeWhile(s => s.Id != listData.DeviceItem).Count();
			}
			EditorGUI.BeginChangeCheck();
			var newIndex = EditorGUI.Popup(cellRect, currentIndex, labels);
			if (EditorGUI.EndChangeCheck() && device != null) {
				if (currentIndex != newIndex) {
					listData.DeviceItem = device.AvailableDeviceItems.ElementAt(newIndex).Id;
					updateAction(listData);
				}
			}
			EditorGUI.EndDisabledGroup();
		}

		protected void UpdateDeviceItem(TListData listData)
		{
			if (listData.DeviceComponent != null && listData.DeviceComponent.AvailableDeviceItems.Count() == 1) {
				listData.DeviceItem = listData.DeviceComponent.AvailableDeviceItems.First().Id;
			}
		}
	}
}
