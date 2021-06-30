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
using Color = UnityEngine.Color;

namespace VisualPinball.Unity.Editor
{
	public class SwitchListViewItemRenderer
	{
		private readonly string[] OPTIONS_SWITCH_SOURCE = { "Input System", "Playfield", "Constant", "Device" };
		private readonly string[] OPTIONS_SWITCH_CONSTANT = { "Closed", "Open" };

		private struct InputSystemEntry
		{
			public string ActionMapName;
			public string ActionName;
		}

		private enum SwitchListColumn
		{
			Id = 0,
			Nc = 1,
			Description = 2,
			Source = 3,
			Element = 4,
			PulseDelay = 5,
		}

		private readonly List<GamelogicEngineSwitch> _gleSwitches;
		private readonly Dictionary<string, ISwitchAuthoring> _switches;
		private readonly Dictionary<string, ISwitchDeviceAuthoring> _switchDevices;
		private readonly InputManager _inputManager;

		private AdvancedDropdownState _itemPickDropdownState;

		public SwitchListViewItemRenderer(List<GamelogicEngineSwitch> gleSwitches, Dictionary<string, ISwitchAuthoring> switches, Dictionary<string, ISwitchDeviceAuthoring> switchDevices, InputManager inputManager)
		{
			_gleSwitches = gleSwitches;
			_switches = switches;
			_switchDevices = switchDevices;
			_inputManager = inputManager;
		}

		public void Render(TableAuthoring tableAuthoring, SwitchListData data, Rect cellRect, int column, Action<SwitchListData> updateAction)
		{
			EditorGUI.BeginDisabledGroup(Application.isPlaying);
			var switchStatuses = Application.isPlaying
				? tableAuthoring.gameObject.GetComponent<Player>()?.SwitchStatusesClosed
				: null;
			switch ((SwitchListColumn)column)
			{
				case SwitchListColumn.Id:
					RenderId(switchStatuses, data, cellRect, updateAction);
					break;
				case SwitchListColumn.Nc:
					RenderNc(data, cellRect, updateAction);
					break;
				case SwitchListColumn.Description:
					RenderDescription(data, cellRect, updateAction);
					break;
				case SwitchListColumn.Source:
					RenderSource(data, cellRect, updateAction);
					break;
				case SwitchListColumn.Element:
					RenderElement(tableAuthoring, data, cellRect, updateAction);
					break;
				case SwitchListColumn.PulseDelay:
					RenderPulseDelay(data, cellRect, updateAction);
					break;
			}
			EditorGUI.EndDisabledGroup();
		}

		private void RenderId(Dictionary<string, bool> switchStatuses, SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
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

			var options = new List<string>(_gleSwitches.Select(entry => entry.Id).ToArray());
			if (options.Count > 0) {
				options.Add("");
			}
			options.Add("Add...");

			if (Application.isPlaying && switchStatuses != null) {
				var iconRect = cellRect;
				iconRect.width = 20;
				dropdownRect.x += 25;
				dropdownRect.width -= 25;
				if (switchStatuses.ContainsKey(switchListData.Id)) {
					var switchStatus = switchStatuses[switchListData.Id];
					var icon = Icons.Switch(switchStatus, IconSize.Small, switchStatus ? IconColor.Orange : IconColor.Gray);
					var guiColor = GUI.color;
					GUI.color = Color.clear;
					EditorGUI.DrawTextureTransparent(iconRect, icon, ScaleMode.ScaleToFit);
					GUI.color = guiColor;
				}
			}

			EditorGUI.BeginChangeCheck();
			var index = EditorGUI.Popup(dropdownRect, options.IndexOf(switchListData.Id), options.ToArray());
			if (EditorGUI.EndChangeCheck()) {
				if (index == options.Count - 1) {
					// "Add..." pressed
					PopupWindow.Show(dropdownRect, new ManagerListTextFieldPopup("ID", "", (newId) => {
						// "Save" pressed
						if (!_gleSwitches.Exists(entry => entry.Id == newId)) {
							_gleSwitches.Add(new GamelogicEngineSwitch(newId));
						}
						switchListData.Id = newId;
						updateAction(switchListData);
					}));

				} else {
					switchListData.Id = _gleSwitches[index].Id;
					updateAction(switchListData);
				}
			}

			EditorGUI.BeginChangeCheck();
			var value = EditorGUI.IntField(idRect, switchListData.InternalId);
			if (EditorGUI.EndChangeCheck()) {
				switchListData.InternalId = value;
				updateAction(switchListData);
			}
		}

		private void RenderNc(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			// don't render for constants
			if (switchListData.Source == SwitchSource.Constant) {
				return;
			}

			// check if it's linked to a switch device, and whether the switch device handles no/nc itself
			var switchDefault = switchListData.Source == SwitchSource.Device
				? _switchDevices.ContainsKey(switchListData.Device.ToLower())
					? _switchDevices[switchListData.Device.ToLower()].SwitchDefault
					: SwitchDefault.Configurable
				: SwitchDefault.Configurable;

			// if it handles it itself, just render the checkbox
			if (switchDefault != SwitchDefault.Configurable) {
				EditorGUI.BeginDisabledGroup(true);
				EditorGUI.Toggle(cellRect, switchDefault == SwitchDefault.NormallyClosed);
				EditorGUI.EndDisabledGroup();
				return;
			}

			// otherwise, let the user toggle
			EditorGUI.BeginChangeCheck();
			var value = EditorGUI.Toggle(cellRect, switchListData.NormallyClosed);
			if (EditorGUI.EndChangeCheck()) {
				switchListData.NormallyClosed = value;
				updateAction(switchListData);
			}
		}

		private void RenderDescription(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var value = EditorGUI.TextField(cellRect, switchListData.Description);
			if (EditorGUI.EndChangeCheck())
			{
				switchListData.Description = value;
				updateAction(switchListData);
			}
		}

		private void RenderSource(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var index = EditorGUI.Popup(cellRect, switchListData.Source, OPTIONS_SWITCH_SOURCE);
			if (EditorGUI.EndChangeCheck())
			{
				if (switchListData.Source != index)
				{
					switchListData.Source = index;
					updateAction(switchListData);
				}
			}
		}

		private void RenderElement(TableAuthoring tableAuthoring, SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			var icon = GetIcon(switchListData);

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

			switch (switchListData.Source)
			{
				case SwitchSource.InputSystem:
					RenderInputSystemElement(switchListData, cellRect, updateAction);
					break;

				case SwitchSource.Playfield:
					RenderPlayfieldElement(tableAuthoring, switchListData, cellRect, updateAction);
					break;

				case SwitchSource.Constant:
					RenderConstantElement(switchListData, cellRect, updateAction);
					break;

				case SwitchSource.Device:
					cellRect.width = cellRect.width / 2f - 5f;
					RenderDeviceElement(tableAuthoring, switchListData, cellRect, updateAction);
					cellRect.x += cellRect.width + 10f;
					RenderDeviceItemElement(switchListData, cellRect, updateAction);
					break;
			}
		}

		private void RenderInputSystemElement(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			var inputSystemList = new List<InputSystemEntry>();
			var tmpIndex = 0;
			var selectedIndex = -1;
			var options = new List<string>();

			foreach (var actionMapName in _inputManager.GetActionMapNames())
			{
				if (options.Count > 0)
				{
					options.Add("");
					inputSystemList.Add(new InputSystemEntry());
					tmpIndex++;
				}

				foreach (var actionName in _inputManager.GetActionNames(actionMapName))
				{
					inputSystemList.Add(new InputSystemEntry
					{
						ActionMapName = actionMapName,
						ActionName = actionName
					});

					options.Add(actionName.Replace('/', '\u2215'));

					if (actionMapName == switchListData.InputActionMap && actionName == switchListData.InputAction)
					{
						selectedIndex = tmpIndex;
					}

					tmpIndex++;
				}
			}

			EditorGUI.BeginChangeCheck();
			var index = EditorGUI.Popup(cellRect, selectedIndex, options.ToArray());
			if (EditorGUI.EndChangeCheck())
			{
				switchListData.InputActionMap = inputSystemList[index].ActionMapName;
				switchListData.InputAction = inputSystemList[index].ActionName;
				updateAction(switchListData);
			}
		}

		private void RenderPlayfieldElement(TableAuthoring tableAuthoring, SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			if (GUI.Button(cellRect, switchListData.PlayfieldItem, EditorStyles.objectField) || GUI.Button(cellRect, "", GUI.skin.GetStyle("IN ObjectField")))
			{
				if (_itemPickDropdownState == null) {
					_itemPickDropdownState = new AdvancedDropdownState();
				}

				var dropdown = new ItemSearchableDropdown<ISwitchAuthoring>(
					_itemPickDropdownState,
					tableAuthoring,
					"Switch Items",
					item => {
						switchListData.PlayfieldItem = item != null ? item.Name : string.Empty;
						updateAction(switchListData);
					}
				);
				dropdown.Show(cellRect);
			}
		}

		private void RenderConstantElement(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var index = EditorGUI.Popup(cellRect, (int)switchListData.Constant, OPTIONS_SWITCH_CONSTANT);
			if (EditorGUI.EndChangeCheck())
			{
				switchListData.Constant = index;
				updateAction(switchListData);
			}
		}

		private void RenderDeviceElement(TableAuthoring tableAuthoring, SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			if (GUI.Button(cellRect, switchListData.Device, EditorStyles.objectField) || GUI.Button(cellRect, "", GUI.skin.GetStyle("IN ObjectField")))
			{
				if (_itemPickDropdownState == null) {
					_itemPickDropdownState = new AdvancedDropdownState();
				}

				var dropdown = new ItemSearchableDropdown<ISwitchDeviceAuthoring>(
					_itemPickDropdownState,
					tableAuthoring,
					"Switch Devices",
					item => {
						switchListData.Device = item != null ? item.Name : string.Empty;
						updateAction(switchListData);
					}
				);
				dropdown.Show(cellRect);
			}
		}

		private void RenderDeviceItemElement(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(switchListData.Device));

			var currentIndex = 0;
			var switchLabels = new string[0];
			ISwitchDeviceAuthoring switchDevice = null;
			if (!string.IsNullOrEmpty(switchListData.Device) && _switchDevices.ContainsKey(switchListData.Device.ToLower())) {
				switchDevice = _switchDevices[switchListData.Device.ToLower()];
				switchLabels = switchDevice.AvailableSwitches.Select(s => s.Description).ToArray();
				currentIndex = switchDevice.AvailableSwitches.TakeWhile(s => s.Id != switchListData.DeviceItem).Count();
			}
			EditorGUI.BeginChangeCheck();
			var newIndex = EditorGUI.Popup(cellRect, currentIndex, switchLabels);
			if (EditorGUI.EndChangeCheck() && switchDevice != null) {
				if (currentIndex != newIndex) {
					switchListData.DeviceItem = switchDevice.AvailableSwitches.ElementAt(newIndex).Id;
					updateAction(switchListData);
				}
			}
			EditorGUI.EndDisabledGroup();
		}

		private void RenderPulseDelay(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			if (switchListData.Source == SwitchSource.Playfield && _switches.ContainsKey(switchListData.PlayfieldItem.ToLower())) {
				var switchable = _switches[switchListData.PlayfieldItem.ToLower()];
				if (switchable.Switchable.IsPulseSwitch) {
					var labelRect = cellRect;
					labelRect.x += labelRect.width - 20;
					labelRect.width = 20;

					var intFieldRect = cellRect;
					intFieldRect.width -= 25;

					EditorGUI.BeginChangeCheck();
					var pulse = EditorGUI.IntField(intFieldRect, switchListData.PulseDelay);
					if (EditorGUI.EndChangeCheck())
					{
						switchListData.PulseDelay = pulse;
						updateAction(switchListData);
					}

					EditorGUI.LabelField(labelRect, "ms");
				}
			}
		}

		private UnityEngine.Texture GetIcon(SwitchListData switchListData)
		{
			Texture2D icon = null;

			switch (switchListData.Source) {
				case SwitchSource.Playfield: {
					if (_switches.ContainsKey(switchListData.PlayfieldItem.ToLower())) {
						icon = Icons.ByComponent(_switches[switchListData.PlayfieldItem.ToLower()], IconSize.Small);
					}
					break;
				}
				case SwitchSource.Constant:
					icon = Icons.Switch(switchListData.Constant == SwitchConstant.Closed, IconSize.Small);
					break;

				case SwitchSource.InputSystem:
					icon = Icons.Key(IconSize.Small);
					break;

				case SwitchSource.Device:
					if (_switchDevices.ContainsKey(switchListData.Device.ToLower())) {
						icon = Icons.ByComponent(_switchDevices[switchListData.Device.ToLower()], IconSize.Small);
					}
					break;
			}

			return icon;
		}
	}
}
