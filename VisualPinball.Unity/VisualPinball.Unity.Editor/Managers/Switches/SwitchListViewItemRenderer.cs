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

namespace VisualPinball.Unity.Editor
{
	public class SwitchListViewItemRenderer
	{
		private readonly string[] OPTIONS_SWITCH_SOURCE = { "Input System", "Playfield", "Constant" };
		private readonly string[] OPTIONS_SWITCH_CONSTANT = { "NC - Normally Closed", "NO - Normally Open" };
		private readonly string[] OPTIONS_SWITCH_TYPE = { "On \u2215 Off", "Pulse" };

		private struct InputSystemEntry
		{
			public string ActionMapName;
			public string ActionName;
		};

		private enum SwitchListColumn
		{
			ID = 0,
			Description = 1,
			Source = 2,
			Element = 3,
			Type = 4,
			Off = 5
		}

		private readonly List<string> _ids;
		private readonly Dictionary<string, ISwitchableAuthoring> _switchables;
		private readonly InputManager _inputManager;

		private AdvancedDropdownState _itemPickDropdownState;

		public SwitchListViewItemRenderer(List<string> ids, Dictionary<string, ISwitchableAuthoring> switchables, InputManager inputManager)
		{
			_ids = ids;
			_switchables = switchables;
			_inputManager = inputManager;
		}

		public void Render(TableAuthoring tableAuthoring, SwitchListData data, Rect cellRect, int column, Action<SwitchListData> updateAction)
		{
			switch ((SwitchListColumn)column)
			{
				case SwitchListColumn.ID:
					RenderID(data, cellRect, updateAction);
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
				case SwitchListColumn.Type:
					RenderType(data, cellRect, updateAction);
					break;
				case SwitchListColumn.Off:
					RenderOff(data, cellRect, updateAction);
					break;
			}
		}

		private void RenderID(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			// add some padding
			cellRect.x += 2;
			cellRect.width -= 4;

			var options = new List<string>(_ids);

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
						if (_ids.IndexOf(newId) == -1)
						{
							_ids.Add(newId);
						}

						switchListData.Id = newId;

						updateAction(switchListData);
					}));
				}
				else
				{
					switchListData.Id = _ids[index];

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

				var dropdown = new ItemSearchableDropdown<ISwitchableAuthoring>(
					_itemPickDropdownState,
					tableAuthoring,
					"Switchable Items",
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

		private void RenderType(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			if (switchListData.Source == SwitchSource.InputSystem || switchListData.Source == SwitchSource.Playfield)
			{
				EditorGUI.BeginChangeCheck();
				var index = EditorGUI.Popup(cellRect, (int)switchListData.Type, OPTIONS_SWITCH_TYPE);
				if (EditorGUI.EndChangeCheck())
				{
					switchListData.Type = index;
					updateAction(switchListData);
				}
			}
		}

		private void RenderOff(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			if (switchListData.Source == SwitchSource.InputSystem || switchListData.Source == SwitchSource.Playfield)
			{
				if (switchListData.Type == SwitchType.Pulse)
				{
					var labelRect = cellRect;
					labelRect.x += labelRect.width - 20;
					labelRect.width = 20;

					var intFieldRect = cellRect;
					intFieldRect.width -= 25;

					EditorGUI.BeginChangeCheck();
					var pulse = EditorGUI.IntField(intFieldRect, switchListData.Pulse);
					if (EditorGUI.EndChangeCheck())
					{
						switchListData.Pulse = pulse;
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
					if (_switchables.ContainsKey(switchListData.PlayfieldItem)) {
						icon = Icons.ByComponent(_switchables[switchListData.PlayfieldItem], size: IconSize.Small);
					}
					break;
				}
				case SwitchSource.Constant:
					icon = Icons.Switch(switchListData.Constant == SwitchConstant.NormallyClosed, size: IconSize.Small);
					break;

				case SwitchSource.InputSystem:
					icon = Icons.Key(IconSize.Small);
					break;
			}

			return icon;
		}
	}
}
