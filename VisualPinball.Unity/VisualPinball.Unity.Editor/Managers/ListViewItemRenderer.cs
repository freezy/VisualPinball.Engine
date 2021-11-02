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
using UnityEngine.InputSystem;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity.Editor
{
	public abstract class ListViewItemRenderer<TListData, TDeviceITem, TStatus> where TListData : IDeviceListData<TDeviceITem> where TDeviceITem : IGamelogicEngineDeviceItem
	{
		protected abstract List<TDeviceITem> GleItems { get; }

		protected abstract TDeviceITem InstantiateGleItem(string id);

		protected abstract Texture2D StatusIcon(TStatus status);

		protected abstract Texture GetIcon(TListData listData);

		protected abstract void RenderDeviceElement(TListData listData, Rect cellRect, Action<TListData> updateAction);

		protected virtual void OnIconClick(TListData data, bool pressedDown)
		{
		}

		protected bool MouseDownOnIcon;

		protected void RenderId(Dictionary<string, TStatus> statuses, ref string id, Action<string> setId, TListData listData, Rect cellRect, Action<TListData> updateAction)
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

				if (Mouse.current.leftButton.wasPressedThisFrame && !MouseDownOnIcon && iconRect.Contains(Event.current.mousePosition)) {
					OnIconClick(listData, true);
					MouseDownOnIcon = true;
				}
				if (Mouse.current.leftButton.wasReleasedThisFrame && MouseDownOnIcon && iconRect.Contains(Event.current.mousePosition)) {
					OnIconClick(listData, false);
					MouseDownOnIcon = false;
				}

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

		protected void RenderDevice(TListData listData, Rect cellRect, Action<TListData> updateAction)
		{
			if (listData != null && !(listData.DeviceComponent as Component)) {
				listData.ClearDevice();
			}

			cellRect = RenderIcon(listData, cellRect);
			cellRect.width = cellRect.width / 2f - 3f;
			RenderDeviceElement(listData, cellRect, updateAction);
			cellRect.x += cellRect.width + 6f;
			RenderDeviceItemElement(listData, cellRect, updateAction);
		}

		private void RenderDeviceItemElement(TListData listData, Rect cellRect, Action<TListData> updateAction)
		{
			// sets the item if only one coil
			UpdateDeviceItem(listData);

			var numDeviceItems = listData.DeviceComponent == null ? -1 : listData.DeviceComponent.AvailableDeviceItems.Count();

			// no coils: show error
			if (numDeviceItems == 0) {
				var icon = EditorGUIUtility.IconContent("Error@2x").image;
				var iconRect = cellRect;
				iconRect.width = 20;
				var guiColor = GUI.color;
				GUI.color = Color.clear;
				EditorGUI.DrawTextureTransparent(iconRect, icon, ScaleMode.ScaleToFit);
				GUI.color = guiColor;
				cellRect.x += 20;

				var s = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.431f, 0.25f) } };
				EditorGUI.LabelField(cellRect, "No coils!", s);
				return;
			}

			// only one coil with no description: show nothing
			if (numDeviceItems == 1 && string.IsNullOrEmpty(listData.DeviceComponent!.AvailableDeviceItems.First().Description)) {
				return;
			}

			// disable if nothing to select
			EditorGUI.BeginDisabledGroup(numDeviceItems <= 1);

			var currentIndex = 0;
			var labels = Array.Empty<string>();
			IDeviceComponent<TDeviceITem> device = null;
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
			var numDeviceItems = listData.DeviceComponent == null ? -1 : listData.DeviceComponent.AvailableDeviceItems.Count();
			listData.DeviceItem = numDeviceItems switch {
				1 => listData.DeviceComponent!.AvailableDeviceItems.First().Id,
				0 => null,
				_ => listData.DeviceItem
			};
		}

		protected Rect RenderIcon(TListData listData, Rect cellRect)
		{
			var icon = GetIcon(listData);
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

			return cellRect;
		}
	}
}
