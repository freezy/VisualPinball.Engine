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

namespace VisualPinball.Unity.Editor
{
	public class WireListViewItemRenderer
	{
		private readonly string[] OPTIONS_SOURCE = { "Input System", "Playfield", "Constant", "Device" };
		private readonly string[] OPTIONS_SOURCE_CONSTANT = { "On", "Off" };

		private readonly string[] OPTIONS_DESTINATION = { "Playfield", "Device" };

		private struct InputSystemEntry
		{
			public string ActionMapName;
			public string ActionName;
		};

		private enum WireListColumn
		{
			Description = 0,
			Source = 1,
			SourceElement = 2,
			Destination = 3,
			DestinationElement = 4,
			PulseDelay = 5,
		}

		private readonly Dictionary<string, ISwitchAuthoring> _switches;
		private readonly Dictionary<string, ISwitchDeviceAuthoring> _switchDevices;
		private readonly InputManager _inputManager;
		private AdvancedDropdownState _sourceElementDeviceDropdownState;

		private readonly Dictionary<string, ICoilAuthoring> _coils;
		private readonly Dictionary<string, ICoilDeviceAuthoring> _coilDevices;
		private AdvancedDropdownState _destinationElementDeviceDropdownState;

		public WireListViewItemRenderer(Dictionary<string, ISwitchAuthoring> switches, Dictionary<string, ISwitchDeviceAuthoring> switchDevices, InputManager inputManager, Dictionary<string, ICoilAuthoring> coils, Dictionary<string, ICoilDeviceAuthoring> coilDevices)
		{
			_switches = switches;
			_switchDevices = switchDevices;
			_inputManager = inputManager;

			_coils = coils;
			_coilDevices = coilDevices;
		}

		public void Render(TableAuthoring tableAuthoring, WireListData data, Rect cellRect, int column, Action<WireListData> updateAction)
		{
			switch ((WireListColumn)column)
			{
				case WireListColumn.Description:
					RenderDescription(data, cellRect, updateAction);
					break;
				case WireListColumn.Source:
					RenderSource(data, cellRect, updateAction);
					break;
				case WireListColumn.SourceElement:
					RenderSourceElement(tableAuthoring, data, cellRect, updateAction);
					break;
				case WireListColumn.Destination:
					RenderDestination(data, cellRect, updateAction);
					break;
				case WireListColumn.DestinationElement:
					RenderDestinationElement(tableAuthoring, data, cellRect, updateAction);
					break;
				case WireListColumn.PulseDelay:
					RenderPulseDelay(data, cellRect, updateAction);
					break;
			}
		}

		private void RenderDescription(WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var value = EditorGUI.TextField(cellRect, wireListData.Description);
			if (EditorGUI.EndChangeCheck())
			{
				wireListData.Description = value;
				updateAction(wireListData);
			}
		}

		private void RenderSource(WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var index = EditorGUI.Popup(cellRect, wireListData.Source, OPTIONS_SOURCE);
			if (EditorGUI.EndChangeCheck())
			{
				if (wireListData.Source != index)
				{
					wireListData.Source = index;
					updateAction(wireListData);
				}
			}
		}

		private void RenderSourceElement(TableAuthoring tableAuthoring, WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			var icon = GetSourceIcon(wireListData);

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

			switch (wireListData.Source)
			{
				case SwitchSource.InputSystem:
					RenderSourceElementInputSystem(wireListData, cellRect, updateAction);
					break;

				case SwitchSource.Playfield:
					RenderSourceElementPlayfield(tableAuthoring, wireListData, cellRect, updateAction);
					break;

				case SwitchSource.Constant:
					RenderSourceElementConstant(wireListData, cellRect, updateAction);
					break;

				case SwitchSource.Device:
					cellRect.width = cellRect.width / 2f - 5f;
					RenderSourceElementDevice(tableAuthoring, wireListData, cellRect, updateAction);
					cellRect.x += cellRect.width + 10f;
					RenderSourceElementDeviceItem(wireListData, cellRect, updateAction);
					break;
			}
		}

		private void RenderSourceElementInputSystem(WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
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

					if (actionMapName == wireListData.SourceInputActionMap && actionName == wireListData.SourceInputAction)
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
				wireListData.SourceInputActionMap = inputSystemList[index].ActionMapName;
				wireListData.SourceInputAction = inputSystemList[index].ActionName;
				updateAction(wireListData);
			}
		}

		private void RenderSourceElementPlayfield(TableAuthoring tableAuthoring, WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			if (GUI.Button(cellRect, wireListData.SourcePlayfieldItem, EditorStyles.objectField) || GUI.Button(cellRect, "", GUI.skin.GetStyle("IN ObjectField")))
			{
				if (_sourceElementDeviceDropdownState == null)
				{
					_sourceElementDeviceDropdownState = new AdvancedDropdownState();
				}

				var dropdown = new ItemSearchableDropdown<ISwitchAuthoring>(
					_sourceElementDeviceDropdownState,
					tableAuthoring,
					"Switch Items",
					item => {
						wireListData.SourcePlayfieldItem = item != null ? item.Name : string.Empty;
						updateAction(wireListData);
					}
				);
				dropdown.Show(cellRect);
			}
		}

		private void RenderSourceElementConstant(WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var index = EditorGUI.Popup(cellRect, (int)wireListData.SourceConstant, OPTIONS_SOURCE_CONSTANT);
			if (EditorGUI.EndChangeCheck())
			{
				wireListData.SourceConstant = index;
				updateAction(wireListData);
			}
		}

		private void RenderSourceElementDevice(TableAuthoring tableAuthoring, WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			if (GUI.Button(cellRect, wireListData.SourceDevice, EditorStyles.objectField) || GUI.Button(cellRect, "", GUI.skin.GetStyle("IN ObjectField")))
			{
				if (_sourceElementDeviceDropdownState == null)
				{
					_sourceElementDeviceDropdownState = new AdvancedDropdownState();
				}

				var dropdown = new ItemSearchableDropdown<ISwitchDeviceAuthoring>(
					_sourceElementDeviceDropdownState,
					tableAuthoring,
					"Switch Devices",
					item => {
						wireListData.SourceDevice = item != null ? item.Name : string.Empty;
						updateAction(wireListData);
					}
				);
				dropdown.Show(cellRect);
			}
		}

		private void RenderSourceElementDeviceItem(WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(wireListData.SourceDevice));

			var currentIndex = 0;
			var switchLabels = new string[0];
			ISwitchDeviceAuthoring switchDevice = null;
			if (!string.IsNullOrEmpty(wireListData.SourceDevice) && _switchDevices.ContainsKey(wireListData.SourceDevice.ToLower()))
			{
				switchDevice = _switchDevices[wireListData.SourceDevice.ToLower()];
				switchLabels = switchDevice.AvailableSwitches.Select(s => s.Description).ToArray();
				currentIndex = switchDevice.AvailableSwitches.TakeWhile(s => s.Id != wireListData.SourceDeviceItem).Count();
			}
			EditorGUI.BeginChangeCheck();
			var newIndex = EditorGUI.Popup(cellRect, currentIndex, switchLabels);
			if (EditorGUI.EndChangeCheck() && switchDevice != null)
			{
				if (currentIndex != newIndex)
				{
					wireListData.SourceDeviceItem = switchDevice.AvailableSwitches.ElementAt(newIndex).Id;
					updateAction(wireListData);
				}
			}
			EditorGUI.EndDisabledGroup();
		}

		private void RenderDestination(WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var index = EditorGUI.Popup(cellRect, wireListData.Destination, OPTIONS_DESTINATION);
			if (EditorGUI.EndChangeCheck())
			{
				if (wireListData.Destination != index)
				{
					wireListData.Destination = index;
					updateAction(wireListData);
				}
			}
		}

		private void RenderDestinationElement(TableAuthoring tableAuthoring, WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			var icon = GetDestinationIcon(wireListData);

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

			switch (wireListData.Destination)
			{
				case CoilDestination.Playfield:
					RenderDestinationElementPlayfield(tableAuthoring, wireListData, cellRect, updateAction);
					break;

				case CoilDestination.Device:
					cellRect.width = cellRect.width / 2f - 5f;
					RenderDestinationElementDevice(tableAuthoring, wireListData, cellRect, updateAction);
					cellRect.x += cellRect.width + 10f;
					RenderDestinationElementDeviceItem(wireListData, cellRect, updateAction);
					break;
			}
		}

		private void RenderDestinationElementPlayfield(TableAuthoring tableAuthoring, WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			if (GUI.Button(cellRect, wireListData.DestinationPlayfieldItem, EditorStyles.objectField) || GUI.Button(cellRect, "", GUI.skin.GetStyle("IN ObjectField")))
			{
				if (_destinationElementDeviceDropdownState == null)
				{
					_destinationElementDeviceDropdownState = new AdvancedDropdownState();
				}

				var dropdown = new ItemSearchableDropdown<ICoilAuthoring>(
					_destinationElementDeviceDropdownState,
					tableAuthoring,
					"Coil Items",
					item => {
						wireListData.DestinationPlayfieldItem = item != null ? item.Name : string.Empty;
						updateAction(wireListData);
					}
				);
				dropdown.Show(cellRect);
			}
		}

		private void RenderDestinationElementDevice(TableAuthoring tableAuthoring, WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			if (GUI.Button(cellRect, wireListData.DestinationDevice, EditorStyles.objectField) || GUI.Button(cellRect, "", GUI.skin.GetStyle("IN ObjectField")))
			{
				if (_destinationElementDeviceDropdownState == null)
				{
					_destinationElementDeviceDropdownState = new AdvancedDropdownState();
				}

				var dropdown = new ItemSearchableDropdown<ICoilDeviceAuthoring>(
					_destinationElementDeviceDropdownState,
					tableAuthoring,
					"Coil Devices",
					item => {
						wireListData.DestinationDevice = item != null ? item.Name : string.Empty;
						updateAction(wireListData);
					}
				);
				dropdown.Show(cellRect);
			}
		}

		private void RenderDestinationElementDeviceItem(WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(wireListData.DestinationDevice));

			var currentIndex = 0;
			var coilLabels = new string[0];
			ICoilDeviceAuthoring coilDevice = null;
			if (!string.IsNullOrEmpty(wireListData.DestinationDevice) && _coilDevices.ContainsKey(wireListData.DestinationDevice.ToLower()))
			{
				coilDevice = _coilDevices[wireListData.DestinationDevice.ToLower()];
				coilLabels = coilDevice.AvailableCoils.Select(s => s.Description).ToArray();
				currentIndex = coilDevice.AvailableCoils.TakeWhile(s => s.Id != wireListData.DestinationDeviceItem).Count();
			}
			EditorGUI.BeginChangeCheck();
			var newIndex = EditorGUI.Popup(cellRect, currentIndex, coilLabels);
			if (EditorGUI.EndChangeCheck() && coilDevice != null)
			{
				if (currentIndex != newIndex)
				{
					wireListData.DestinationDeviceItem = coilDevice.AvailableCoils.ElementAt(newIndex).Id;
					updateAction(wireListData);
				}
			}
			EditorGUI.EndDisabledGroup();
		}

		private void RenderPulseDelay(WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			if (wireListData.Source == SwitchSource.Playfield && _switches.ContainsKey(wireListData.SourcePlayfieldItem.ToLower()))
			{
				var switchable = _switches[wireListData.SourcePlayfieldItem.ToLower()];
				if (switchable.Switchable.IsPulseSwitch)
				{
					var labelRect = cellRect;
					labelRect.x += labelRect.width - 20;
					labelRect.width = 20;

					var intFieldRect = cellRect;
					intFieldRect.width -= 25;

					EditorGUI.BeginChangeCheck();
					var pulse = EditorGUI.IntField(intFieldRect, wireListData.PulseDelay);
					if (EditorGUI.EndChangeCheck())
					{
						wireListData.PulseDelay = pulse;
						updateAction(wireListData);
					}

					EditorGUI.LabelField(labelRect, "ms");
				}
			}
		}

		private UnityEngine.Texture GetSourceIcon(WireListData wireListData)
		{
			Texture2D icon = null;

			switch (wireListData.Source)
			{
				case SwitchSource.Playfield:
					{
						if (_switches.ContainsKey(wireListData.SourcePlayfieldItem.ToLower()))
						{
							icon = Icons.ByComponent(_switches[wireListData.SourcePlayfieldItem.ToLower()], IconSize.Small);
						}
						break;
					}
				case SwitchSource.Constant:
					icon = Icons.Switch(wireListData.SourceConstant == SwitchConstant.NormallyClosed, IconSize.Small);
					break;

				case SwitchSource.InputSystem:
					icon = Icons.Key(IconSize.Small);
					break;

				case SwitchSource.Device:
					if (_switchDevices.ContainsKey(wireListData.SourceDevice.ToLower()))
					{
						icon = Icons.ByComponent(_switchDevices[wireListData.SourceDevice.ToLower()], IconSize.Small);
					}
					break;
			}

			return icon;
		}

		private UnityEngine.Texture GetDestinationIcon(WireListData wireListData)
		{
			Texture2D icon = null;

			switch (wireListData.Destination)
			{
				case CoilDestination.Playfield:
					if (_coils.ContainsKey(wireListData.DestinationPlayfieldItem.ToLower())) {
						icon = Icons.ByComponent(_coils[wireListData.DestinationPlayfieldItem.ToLower()], size: IconSize.Small);
					}
					break;

				case CoilDestination.Device:
					if (_coilDevices.ContainsKey(wireListData.DestinationDevice.ToLower())) {
						icon = Icons.ByComponent(_coilDevices[wireListData.DestinationDevice.ToLower()], IconSize.Small);
					}
					break;
			}

			return icon;
		}
	}
}
