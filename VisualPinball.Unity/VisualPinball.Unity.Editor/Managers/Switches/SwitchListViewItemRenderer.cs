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
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	public class SwitchListViewItemRenderer
	{
		private readonly string ICON_PATH = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/Resources/Icons";

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

		private List<string> _ids;
		private List<ISwitchableAuthoring> _switchables;
		private InputManager _inputManager;

		public SwitchListViewItemRenderer(List<string> ids, List<ISwitchableAuthoring> switchables, InputManager inputManager)
		{
			_ids = ids;
			_switchables = switchables;
			_inputManager = inputManager;
		}

		public void Render(SwitchListData data, Rect cellRect, int column, Action<SwitchListData> updateAction)
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
					RenderElement(data, cellRect, updateAction);
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
			var options = new List<string>(_ids);

			if (options.Count > 0)
			{
				options.Add("");
			}

			options.Add("Add...");

			EditorGUI.BeginChangeCheck();
			int index = EditorGUI.Popup(cellRect, options.IndexOf(switchListData.ID), options.ToArray());
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

						switchListData.ID = newId;

						updateAction(switchListData);
					}));
				}
				else
				{
					switchListData.ID = _ids[index];

					updateAction(switchListData);
				}
			}
		}

		private void RenderDescription(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			var icon = GetIcon(switchListData);

			if (icon != null)
			{
				var iconRect = cellRect;
				iconRect.width = 20;
				EditorGUI.DrawTextureAlpha(iconRect, icon, ScaleMode.ScaleToFit);
			}

			var textFieldRect = cellRect;
			textFieldRect.x += 25;
			textFieldRect.width -= 25;

			EditorGUI.BeginChangeCheck();
			var value = EditorGUI.TextField(textFieldRect, switchListData.Description);
			if (EditorGUI.EndChangeCheck())
			{
				switchListData.Description = value;
				updateAction(switchListData);
			}
		}

		private void RenderSource(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			int index = EditorGUI.Popup(cellRect, switchListData.Source, OPTIONS_SWITCH_SOURCE);
			if (EditorGUI.EndChangeCheck())
			{
				if (switchListData.Source != index)
				{
					switchListData.Source = index;

					if (switchListData.Source != SwitchSource.Playfield)
					{
						switchListData.Description = "";
					}

					updateAction(switchListData);
				}
			}
		}

		private void RenderElement(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			switch (switchListData.Source)
			{
				case SwitchSource.InputSystem:
					{
						List<InputSystemEntry> inputSystemList = new List<InputSystemEntry>();

						var tmpIndex = 0;
						var selectedIndex = -1;
						
						List<string> options = new List<string>();

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
						int index = EditorGUI.Popup(cellRect, selectedIndex, options.ToArray());
						if (EditorGUI.EndChangeCheck())
						{
							switchListData.InputActionMap = inputSystemList[index].ActionMapName;
							switchListData.InputAction = inputSystemList[index].ActionName;
							updateAction(switchListData);
						}
					}
					break;
				case SwitchSource.Playfield:
					{
						List<string> options = new List<string>();
						foreach (var item in _switchables)
						{
							options.Add(item.Name);
						}

						EditorGUI.BeginChangeCheck();
						int index = EditorGUI.Popup(cellRect, options.IndexOf(switchListData.PlayfieldItem), options.ToArray());
						if (EditorGUI.EndChangeCheck())
						{
							if (index != options.IndexOf(switchListData.PlayfieldItem))
							{
								switchListData.PlayfieldItem = options[index];
								switchListData.Description = _switchables[index].DefaultDescription;
								updateAction(switchListData);
							}
						}
					}
					break;
				case SwitchSource.Constant:
					{
						EditorGUI.BeginChangeCheck();
						int index = EditorGUI.Popup(cellRect, (int)switchListData.Constant, OPTIONS_SWITCH_CONSTANT);
						if (EditorGUI.EndChangeCheck())
						{
							switchListData.Constant = index;
							updateAction(switchListData);
						}
					}
					break;
			}
		}

		private void RenderType(SwitchListData switchListData, Rect cellRect, Action<SwitchListData> updateAction)
		{
			if (switchListData.Source == SwitchSource.InputSystem || switchListData.Source == SwitchSource.Playfield)
			{
				EditorGUI.BeginChangeCheck();
				int index = EditorGUI.Popup(cellRect, (int)switchListData.Type, OPTIONS_SWITCH_TYPE);
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
			string image = null;

			if (switchListData.Source == SwitchSource.Playfield)
			{
				foreach (var item in _switchables)
				{
					if (item.Name == switchListData.PlayfieldItem)
					{
						image = item.IconName;
					}
				}
			}
			else if (switchListData.Source == SwitchSource.Constant)
			{
				image = "switch_" +
					(switchListData.Constant == SwitchConstant.NormallyClosed ? "nc" : "no");
			}

			if (image != null)
			{
				return AssetDatabase.LoadAssetAtPath<Texture2D>($"{ICON_PATH}/icon_" + image + ".png");
			}

			return null;
		}
	}
}