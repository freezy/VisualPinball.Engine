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
using System.Linq;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity.Editor
{
	public class CoilListViewItemRenderer
	{
		private readonly string[] OPTIONS_COIL_DESTINATION = { "Playfield", "Device" };
		private readonly string[] OPTIONS_COIL_TYPE = { "Single-Wound", "Dual-Wound" };

		private enum CoilListColumn
		{
			Id = 0,
			Description = 1,
			Destination = 2,
			Element = 3,
			Type = 4,
			HoldCoilId = 5,
		}

		private readonly List<GamelogicEngineCoil> _gleCoils;
		private readonly Dictionary<string, ICoilAuthoring> _coils;
		private readonly Dictionary<string, ICoilDeviceAuthoring> _coilDevices;

		private AdvancedDropdownState _itemPickDropdownState;

		public CoilListViewItemRenderer(List<GamelogicEngineCoil> gleCoils, Dictionary<string, ICoilAuthoring> coils, Dictionary<string, ICoilDeviceAuthoring> coilDevices)
		{
			_gleCoils = gleCoils;
			_coils = coils;
			_coilDevices = coilDevices;
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

			var options = new List<string>(_gleCoils.Select(entry => entry.Id).ToArray());

			if (options.Count > 0) {
				options.Add("");
			}

			options.Add("Add...");

			EditorGUI.BeginChangeCheck();
			var index = EditorGUI.Popup(cellRect, options.IndexOf(id), options.ToArray());
			if (EditorGUI.EndChangeCheck()) {
				if (index == options.Count - 1) {
					PopupWindow.Show(cellRect, new ManagerListTextFieldPopup("ID", "", newId => {
						if (_gleCoils.Exists(entry => entry.Id == newId))
						{
							_gleCoils.Add(new GamelogicEngineCoil
							{
								Id = newId
							});
						}

						setId(newId);
						updateAction(coilListData);
					}));

				} else {
					setId(_gleCoils[index].Id);
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

				case CoilDestination.Device:
					cellRect.width = cellRect.width / 2f - 5f;
					RenderDeviceElement(tableAuthoring, coilListData, cellRect, updateAction);
					cellRect.x += cellRect.width + 10f;
					RenderDeviceItemElement(coilListData, cellRect, updateAction);
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

		private void RenderDeviceElement(TableAuthoring tableAuthoring, CoilListData coilListData, Rect cellRect, Action<CoilListData> updateAction)
		{
			if (GUI.Button(cellRect, coilListData.Device, EditorStyles.objectField) || GUI.Button(cellRect, "", GUI.skin.GetStyle("IN ObjectField")))
			{
				if (_itemPickDropdownState == null) {
					_itemPickDropdownState = new AdvancedDropdownState();
				}

				var dropdown = new ItemSearchableDropdown<ICoilDeviceAuthoring>(
					_itemPickDropdownState,
					tableAuthoring,
					"Coil Devices",
					item => {
						coilListData.Device = item.Name;
						updateAction(coilListData);
					}
				);
				dropdown.Show(cellRect);
			}
		}

		private void RenderDeviceItemElement(CoilListData coilListData, Rect cellRect, Action<CoilListData> updateAction)
		{
			EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(coilListData.Device));

			var currentIndex = 0;
			var coilLabels = new string[0];
			ICoilDeviceAuthoring coilDevice = null;
			if (!string.IsNullOrEmpty(coilListData.Device) && _coilDevices.ContainsKey(coilListData.Device.ToLower())) {
				coilDevice = _coilDevices[coilListData.Device.ToLower()];
				coilLabels = coilDevice.AvailableCoils.Select(s => s.Description).ToArray();
				currentIndex = coilDevice.AvailableCoils.TakeWhile(s => s.Id != coilListData.DeviceItem).Count();
			}
			EditorGUI.BeginChangeCheck();
			var newIndex = EditorGUI.Popup(cellRect, currentIndex, coilLabels);
			if (EditorGUI.EndChangeCheck() && coilDevice != null) {
				if (currentIndex != newIndex) {
					coilListData.DeviceItem = coilDevice.AvailableCoils.ElementAt(newIndex).Id;
					updateAction(coilListData);
				}
			}
			EditorGUI.EndDisabledGroup();
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
					if (_coils.ContainsKey(coilListData.PlayfieldItem.ToLower())) {
						icon = Icons.ByComponent(_coils[coilListData.PlayfieldItem.ToLower()], size: IconSize.Small);
					}
					break;

				case CoilDestination.Device:
					if (_coilDevices.ContainsKey(coilListData.Device.ToLower())) {
						icon = Icons.ByComponent(_coilDevices[coilListData.Device.ToLower()], IconSize.Small);
					}
					break;
			}

			return icon;
		}
	}
}
