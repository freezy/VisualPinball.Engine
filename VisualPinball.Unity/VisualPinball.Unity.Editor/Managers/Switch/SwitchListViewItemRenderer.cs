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
using VisualPinball.Engine.VPT;
using Texture = UnityEngine.Texture;

namespace VisualPinball.Unity.Editor
{
	public class SwitchListViewItemRenderer : ListViewItemRenderer<SwitchListData, GamelogicEngineSwitch>
	{
		protected override List<GamelogicEngineSwitch> GleItems => _gleSwitches;
		protected override GamelogicEngineSwitch InstantiateGleItem(string id) => new GamelogicEngineSwitch(id);
		protected override Texture2D StatusIcon(bool status) => Icons.Switch(status, IconSize.Small, status ? IconColor.Orange : IconColor.Gray);

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
		private readonly InputManager _inputManager;

		private readonly ObjectReferencePicker<ISwitchDeviceAuthoring> _devicePicker;

		public SwitchListViewItemRenderer(List<GamelogicEngineSwitch> gleSwitches, TableAuthoring tableComponent, InputManager inputManager)
		{
			_gleSwitches = gleSwitches;
			_inputManager = inputManager;
			_devicePicker = new ObjectReferencePicker<ISwitchDeviceAuthoring>("Switch Devices", tableComponent, false);
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
					RenderId(switchStatuses, ref data.Id, id => data.Id = id, data, cellRect, updateAction);
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
					RenderElement(data, cellRect, updateAction);
					break;
				case SwitchListColumn.PulseDelay:
					RenderPulseDelay(data, cellRect, updateAction);
					break;
			}
			EditorGUI.EndDisabledGroup();
		}

		private void RenderNc(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			// don't render for constants
			if (switchListData.Source == ESwitchSource.Constant) {
				return;
			}

			// check if it's linked to a switch device, and whether the switch device handles no/nc itself
			var switchDefault = switchListData.Source == ESwitchSource.Playfield
				? switchListData.Device?.SwitchDefault ?? SwitchDefault.Configurable
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

		private void RenderSource(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var source = (ESwitchSource)EditorGUI.EnumPopup(cellRect, switchListData.Source);
			if (EditorGUI.EndChangeCheck()) {
				switchListData.Source = source;
				updateAction(switchListData);
			}
		}

		private void RenderElement(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			switch (switchListData.Source)
			{
				case ESwitchSource.InputSystem:
					cellRect = RenderIcon(switchListData, cellRect);
					RenderInputSystemElement(switchListData, cellRect, updateAction);
					break;

				case ESwitchSource.Playfield:
					var icon = GetIcon(switchListData);
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

					cellRect.width = cellRect.width / 2f - 5f;
					RenderDeviceElement(switchListData, cellRect, updateAction);
					cellRect.x += cellRect.width + 10f;
					RenderDeviceItemElement(switchListData, cellRect, updateAction);

					break;

				case ESwitchSource.Constant:
					cellRect = RenderIcon(switchListData, cellRect);
					RenderConstantElement(switchListData, cellRect, updateAction);
					break;
			}
		}

		private Rect RenderIcon(SwitchListData switchListData, Rect cellRect)
		{
			var icon = GetIcon(switchListData);

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

		private void RenderDeviceElement(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			_devicePicker.Render(cellRect, switchListData.Device, component => {
				switchListData.Device = component;
				UpdateDeviceItem(switchListData);
				updateAction(switchListData);
			});
		}

		private void RenderPulseDelay(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			if (switchListData.Source == ESwitchSource.Playfield && switchListData.Device != null) {
				var switchable = switchListData.Device.AvailableSwitches.FirstOrDefault(sw => sw.Id == switchListData.DeviceItem);
				if (switchable != null && switchable.IsPulseSwitch) {
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

		private Texture GetIcon(SwitchListData switchListData)
		{
			Texture2D icon = null;

			switch (switchListData.Source) {
				case ESwitchSource.Playfield: {
					if (switchListData.Device != null) {
						icon = Icons.ByComponent(switchListData.Device, IconSize.Small);
					}
					break;
				}
				case ESwitchSource.Constant:
					icon = Icons.Switch(switchListData.Constant == Engine.VPT.SwitchConstant.Closed, IconSize.Small);
					break;

				case ESwitchSource.InputSystem:
					icon = Icons.Key(IconSize.Small);
					break;
			}

			return icon;
		}
	}
}
