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
	public class SwitchListViewItemRenderer
	{
		private readonly string[] OPTIONS_SWITCH_SOURCE = { "Input System", "Playfield", "Constant", "Device" };
		private readonly string[] OPTIONS_SWITCH_CONSTANT = { "NC - Normally Closed", "NO - Normally Open" };

		private struct InputSystemEntry
		{
			public string ActionMapName;
			public string ActionName;
		};

		private enum SwitchListColumn
		{
			Id = 0,
			Description = 1,
			Source = 2,
			Element = 3,
			PulseDelay = 4,
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
			switch ((SwitchListColumn)column)
			{
				case SwitchListColumn.Id:
					RenderId(data, cellRect, updateAction);
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
		}

		private void RenderId(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			// add some padding
			cellRect.x += 2;
			cellRect.width -= 4;

			var options = new List<string>(_gleSwitches.Select(entry => entry.Id).ToArray());

			if (options.Count > 0)
			{
				options.Add("");
			}

			options.Add("Add...");

			EditorGUI.BeginChangeCheck();
			var index = EditorGUI.Popup(cellRect, options.IndexOf(switchListData.Id), options.ToArray());
			if (EditorGUI.EndChangeCheck())
			{
				if (index == options.Count - 1)
				{
					PopupWindow.Show(cellRect, new ManagerListTextFieldPopup("ID", "", (newId) =>
					{
						if (_gleSwitches.Exists(entry => entry.Id == newId))
						{
							_gleSwitches.Add(new GamelogicEngineSwitch
							{
								Id = newId
							});
						}

						switchListData.Id = newId;

						updateAction(switchListData);
					}));
				}
				else
				{
					switchListData.Id = _gleSwitches[index].Id;

					updateAction(switchListData);
				}
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
						switchListData.PlayfieldItem = item.Name;
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
						switchListData.Device = item.Name;
						updateAction(switchListData);
					}
				);
				dropdown.Show(cellRect);
			}
		}

		private void RenderDeviceItemElement(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(switchListData.Device));
			var switchLabels = new string[0];
			ISwitchDeviceAuthoring switchDevice = null;
			if (!string.IsNullOrEmpty(switchListData.Device)) {
				switchDevice = _switchDevices[switchListData.Device.ToLower()];
				switchLabels = switchDevice.AvailableSwitches.Select(s => s.Description).ToArray();
			}
			EditorGUI.BeginChangeCheck();
			var index = EditorGUI.Popup(cellRect, switchListData.DeviceItemIndex, switchLabels);
			if (EditorGUI.EndChangeCheck() && switchDevice != null) {
				if (switchListData.DeviceItemIndex != index) {
					switchListData.DeviceItemIndex = index;
					switchListData.DeviceItem = switchDevice.AvailableSwitches[index].Id;
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
					icon = Icons.Switch(switchListData.Constant == SwitchConstant.NormallyClosed, IconSize.Small);
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
