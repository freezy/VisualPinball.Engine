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

namespace VisualPinball.Unity.Editor
{
	public class WireListViewItemRenderer
	{
		private readonly string[] OPTIONS_SOURCE_CONSTANT = { "On", "Off" };

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
			DestinationElement = 3,
			PulseDelay = 4,
		}

		private readonly InputManager _inputManager;

		private readonly ObjectReferencePicker<ISwitchDeviceAuthoring> _sourceDevicePicker;
		private readonly ObjectReferencePicker<ICoilDeviceAuthoring> _destDevicePicker;

		public WireListViewItemRenderer(TableAuthoring tableComponent, InputManager inputManager)
		{
			_inputManager = inputManager;

			_sourceDevicePicker = new ObjectReferencePicker<ISwitchDeviceAuthoring>("Wire Source", tableComponent, false);
			_destDevicePicker = new ObjectReferencePicker<ICoilDeviceAuthoring>("Wire Destination", tableComponent, false);
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
			var source = (ESwitchSource)EditorGUI.EnumPopup(cellRect, wireListData.Source);
			if (EditorGUI.EndChangeCheck()) {
				wireListData.Source = source;
				updateAction(wireListData);
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
				case ESwitchSource.InputSystem:
					RenderSourceElementInputSystem(wireListData, cellRect, updateAction);
					break;

				case ESwitchSource.Playfield:
					cellRect.width = cellRect.width / 2f - 5f;
					RenderSourceElementDevice(wireListData, cellRect, updateAction);
					cellRect.x += cellRect.width + 10f;
					RenderSourceElementDeviceItem(wireListData, cellRect, updateAction);
					break;

				case ESwitchSource.Constant:
					RenderSourceElementConstant(wireListData, cellRect, updateAction);
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

		private void RenderSourceElementDevice(WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			_sourceDevicePicker.Render(cellRect, wireListData.SourceDevice, item => {
				wireListData.SourceDevice = item;
				if (wireListData.SourceDevice != null && wireListData.SourceDevice.AvailableSwitches.Count() == 1) {
					wireListData.SourceDeviceItem = wireListData.SourceDevice.AvailableSwitches.First().Id;
				}
				updateAction(wireListData);
			});
		}

		private void RenderSourceElementDeviceItem(WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			EditorGUI.BeginDisabledGroup(wireListData.SourceDevice == null);

			var currentIndex = 0;
			var switchLabels = Array.Empty<string>();
			if (wireListData.SourceDevice != null)
			{
				switchLabels = wireListData.SourceDevice.AvailableSwitches.Select(s => s.Description).ToArray();
				currentIndex = wireListData.SourceDevice.AvailableSwitches.TakeWhile(s => s.Id != wireListData.SourceDeviceItem).Count();
			}
			EditorGUI.BeginChangeCheck();
			var newIndex = EditorGUI.Popup(cellRect, currentIndex, switchLabels);
			if (EditorGUI.EndChangeCheck() && wireListData.SourceDevice != null)
			{
				if (currentIndex != newIndex)
				{
					wireListData.SourceDeviceItem = wireListData.SourceDevice.AvailableSwitches.ElementAt(newIndex).Id;
					updateAction(wireListData);
				}
			}
			EditorGUI.EndDisabledGroup();
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

			cellRect.width = cellRect.width / 2f - 5f;
			RenderDestinationElementDevice(tableAuthoring, wireListData, cellRect, updateAction);
			cellRect.x += cellRect.width + 10f;
			RenderDestinationElementDeviceItem(wireListData, cellRect, updateAction);
		}

		private void RenderDestinationElementDevice(TableAuthoring tableAuthoring, WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			_destDevicePicker.Render(cellRect, wireListData.DestinationDevice, item => {
				wireListData.DestinationDevice = item;
				if (wireListData.DestinationDevice != null && wireListData.DestinationDevice.AvailableCoils.Count() == 1) {
					wireListData.DestinationDeviceItem = wireListData.DestinationDevice.AvailableCoils.First().Id;
				}
				updateAction(wireListData);
			});
		}

		private void RenderDestinationElementDeviceItem(WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			EditorGUI.BeginDisabledGroup(wireListData.DestinationDevice == null);

			var currentIndex = 0;
			var coilLabels = Array.Empty<string>();
			if (wireListData.DestinationDevice != null) {
				coilLabels = wireListData.DestinationDevice.AvailableCoils.Select(s => s.Description).ToArray();
				currentIndex = wireListData.DestinationDevice.AvailableCoils.TakeWhile(s => s.Id != wireListData.DestinationDeviceItem).Count();
			}
			EditorGUI.BeginChangeCheck();
			var newIndex = EditorGUI.Popup(cellRect, currentIndex, coilLabels);
			if (EditorGUI.EndChangeCheck() && wireListData.DestinationDevice != null)
			{
				if (currentIndex != newIndex)
				{
					wireListData.DestinationDeviceItem = wireListData.DestinationDevice.AvailableCoils.ElementAt(newIndex).Id;
					updateAction(wireListData);
				}
			}
			EditorGUI.EndDisabledGroup();
		}

		private void RenderPulseDelay(WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			if (wireListData.SourceDevice != null && !string.IsNullOrEmpty(wireListData.SourceDeviceItem)) {
				var switchable = wireListData.SourceDevice.AvailableSwitches.First(s => s.Id == wireListData.SourceDeviceItem);
				if (switchable.IsPulseSwitch)
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
				case ESwitchSource.Playfield:
					if (wireListData.SourceDevice != null) {
						icon = Icons.ByComponent(wireListData.SourceDevice, IconSize.Small);
					}
					break;

				case ESwitchSource.Constant:
					icon = Icons.Switch(wireListData.SourceConstant == SwitchConstant.Closed, IconSize.Small);
					break;

				case SwitchSource.InputSystem:
					icon = Icons.Key(IconSize.Small);
					break;
			}

			return icon;
		}

		private UnityEngine.Texture GetDestinationIcon(WireListData wireListData)
		{
			Texture2D icon = null;

			if (wireListData.DestinationDevice != null) {
				icon = Icons.ByComponent(wireListData.DestinationDevice, IconSize.Small);
			}

			return icon;
		}
	}
}
