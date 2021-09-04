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
	public class WireListViewItemRenderer: ListViewItemRenderer<WireListData, IGamelogicEngineDeviceItem, bool>
	{
		protected override List<IGamelogicEngineDeviceItem> GleItems => new List<IGamelogicEngineDeviceItem>();
		protected override IGamelogicEngineDeviceItem InstantiateGleItem(string id) => null;
		protected override Texture2D StatusIcon(bool status) => null;

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
		private readonly ObjectReferencePicker<IWireableAuthoring> _destDevicePicker;

		public WireListViewItemRenderer(TableAuthoring tableComponent, InputManager inputManager)
		{
			_inputManager = inputManager;

			_sourceDevicePicker = new ObjectReferencePicker<ISwitchDeviceAuthoring>("Wire Source", tableComponent, false);
			_destDevicePicker = new ObjectReferencePicker<IWireableAuthoring>("Wire Destination", tableComponent, false);
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
					RenderSourceElement(data, cellRect, updateAction);
					break;
				case WireListColumn.DestinationElement:
					RenderDestinationElement(data, cellRect, updateAction);
					break;
				case WireListColumn.PulseDelay:
					RenderPulseDelay(data, cellRect, updateAction);
					break;
			}
		}

		private void RenderSource(WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var source = (SwitchSource)EditorGUI.EnumPopup(cellRect, wireListData.Source);
			if (EditorGUI.EndChangeCheck()) {
				wireListData.Source = source;
				updateAction(wireListData);
			}
		}

		private void RenderSourceElement(WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
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
					cellRect.width = cellRect.width / 2f - 5f;
					RenderSourceElementDevice(wireListData, cellRect, updateAction);
					cellRect.x += cellRect.width + 10f;
					RenderSourceElementDeviceItem(wireListData, cellRect, updateAction);
					break;

				case SwitchSource.Constant:
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
			var constVal = (SwitchConstant)EditorGUI.EnumPopup(cellRect, wireListData.SourceConstant);
			if (EditorGUI.EndChangeCheck()) {
				wireListData.SourceConstant = constVal;
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

		private void RenderDestinationElement(WireListData wireListData, Rect cellRect, Action<WireListData> updateAction)
		{
			RenderDevice(wireListData, cellRect, updateAction);
		}

		protected override void RenderDeviceElement(WireListData listData, Rect cellRect, Action<WireListData> updateAction)
		{
			_destDevicePicker.Render(cellRect, listData.DestinationDevice, item => {
				listData.DestinationDevice = item;
				if (listData.DestinationDevice != null && listData.DestinationDevice.AvailableWireDestinations.Count() == 1) {
					listData.DestinationDeviceItem = listData.DestinationDevice.AvailableWireDestinations.First().Id;
				}
				updateAction(listData);
			});
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

		private Texture GetSourceIcon(WireListData wireListData)
		{
			Texture2D icon = null;

			switch (wireListData.Source)
			{
				case SwitchSource.Playfield:
					if (wireListData.SourceDevice != null) {
						icon = Icons.ByComponent(wireListData.SourceDevice, IconSize.Small);
					}
					break;

				case SwitchSource.Constant:
					icon = Icons.Switch(wireListData.SourceConstant == SwitchConstant.Closed, IconSize.Small);
					break;

				case SwitchSource.InputSystem:
					icon = Icons.Key(IconSize.Small);
					break;
			}

			return icon;
		}

		protected override Texture GetIcon(WireListData listData)
		{
			return listData.DestinationDevice != null ? Icons.ByComponent(listData.DestinationDevice, IconSize.Small) : null;
		}

	}
}
