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

namespace VisualPinball.Unity.Editor
{
	public class SwitchListViewItemRenderer
	{
		string ICON_PATH = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/Resources/Icons";

		string[] OPTIONS_SWITCH_SOURCE = { "Input System", "Playfield", "Constant" };
		string[] OPTIONS_SWITCH_CONSTANT = { "NC - Normally Closed", "NO - Normally Open" };
		string[] OPTIONS_SWITCH_TYPE = { "On \u2215 Off", "Pulse" };
		string[] OPTIONS_SWITCH_TRIGGER_INPUT_SYSTEM = { "KeyDown", "KeyUp" };
		string[] OPTIONS_SWITCH_TRIGGER_PLAYFIELD = { "Hit", "UnHit" };

		List<string> _ids;
		List<ISwitchableAuthoring> _switchables;

		public enum SwitchListColumn
		{
			ID = 0,
			Description = 1,
			Source = 2,
			Element = 3,
			Type = 4,
			Trigger = 5,
			Off = 6
		}

		public SwitchListViewItemRenderer(List<string> ids, List<ISwitchableAuthoring> switchables)
		{
			_ids = ids;
			_switchables = switchables;
		}

		public void Render(SwitchListData data, Rect cellRect, int column, Action updateAction)
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
				case SwitchListColumn.Trigger:
					RenderTrigger(data, cellRect, updateAction);
					break;
				case SwitchListColumn.Off:
					RenderOff(data, cellRect, updateAction);
					break;
			}
		}

		private void RenderID(SwitchListData switchListData, Rect cellRect, Action updateAction)
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

						updateAction();
					}));
				}
				else
				{
					switchListData.ID = _ids[index];

					updateAction();
				}
			}
		}

		private void RenderDescription(SwitchListData switchListData, Rect cellRect, Action updateAction)
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
				updateAction();
			}
		}

		private void RenderSource(SwitchListData switchListData, Rect cellRect, Action updateAction)
		{
			EditorGUI.BeginChangeCheck();
			int index = EditorGUI.Popup(cellRect, (int)switchListData.Source, OPTIONS_SWITCH_SOURCE);
			if (EditorGUI.EndChangeCheck())
			{
				switchListData.Source = (SwitchSource)index;

				if (switchListData.Source == SwitchSource.InputSystem)
				{
					switch (switchListData.Trigger)
					{
						case SwitchEvent.Hit:
							switchListData.Trigger = SwitchEvent.KeyDown;
							break;

						case SwitchEvent.UnHit:
							switchListData.Trigger = SwitchEvent.KeyUp;
							break;

						case SwitchEvent.None:
							switchListData.Trigger = SwitchEvent.KeyDown;
							break;
					}
				}
				else if (switchListData.Source == SwitchSource.Playfield)
				{
					switch (switchListData.Trigger)
					{
						case SwitchEvent.KeyDown:
							switchListData.Trigger = SwitchEvent.Hit;
							break;

						case SwitchEvent.KeyUp:
							switchListData.Trigger = SwitchEvent.UnHit;
							break;

						case SwitchEvent.None:
							switchListData.Trigger = SwitchEvent.Hit;
							break;
					}
				}

				updateAction();
			}
		}

		private void RenderElement(SwitchListData switchListData, Rect cellRect, Action updateAction)
		{
			switch (switchListData.Source)
			{
				case SwitchSource.InputSystem:
					{
						string[] options = new string[] {
							"Left Flipper", "Right Flipper", "Left Magna Save", "Right Magna Save", "Start", "Plunger",
							"Coin Door 1", "Coin Door 2", "Coin Door 3", "Coin Door 4" };
						EditorGUI.BeginChangeCheck();
						int index = EditorGUI.Popup(cellRect, Array.IndexOf(options, switchListData.Element), options);
						if (EditorGUI.EndChangeCheck())
						{
							switchListData.Element = options[index];
							updateAction();
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
						int index = EditorGUI.Popup(cellRect, options.IndexOf(switchListData.Element), options.ToArray());
						if (EditorGUI.EndChangeCheck())
						{
							switchListData.Element = options[index];
							updateAction();
						}
					}
					break;
				case SwitchSource.Constant:
					{
						EditorGUI.BeginChangeCheck();
						int index = EditorGUI.Popup(cellRect, (int)switchListData.Constant, OPTIONS_SWITCH_CONSTANT);
						if (EditorGUI.EndChangeCheck())
						{
							switchListData.Constant = (SwitchConstant)index;
							updateAction();
						}
					}
					break;
			}
		}

		private void RenderType(SwitchListData switchListData, Rect cellRect, Action updateAction)
		{
			if (switchListData.Source == SwitchSource.InputSystem || switchListData.Source == SwitchSource.Playfield)
			{
				EditorGUI.BeginChangeCheck();
				int index = EditorGUI.Popup(cellRect, (int)switchListData.Type, OPTIONS_SWITCH_TYPE);
				if (EditorGUI.EndChangeCheck())
				{
					switchListData.Type = (SwitchType)index;
					updateAction();
				}
			}
		}

		private void RenderTrigger(SwitchListData switchListData, Rect cellRect, Action updateAction)
		{
			if (switchListData.Source == SwitchSource.InputSystem)
			{
				EditorGUI.BeginChangeCheck();
				int index = EditorGUI.Popup(cellRect, (switchListData.Trigger == SwitchEvent.KeyUp) ? 1 : 0, OPTIONS_SWITCH_TRIGGER_INPUT_SYSTEM);
				if (EditorGUI.EndChangeCheck())
				{
					switchListData.Trigger = (index == 1) ? SwitchEvent.KeyUp : SwitchEvent.KeyDown;
					updateAction();
				}
			}
			else if (switchListData.Source == SwitchSource.Playfield)
			{
				int index = EditorGUI.Popup(cellRect, (switchListData.Trigger == SwitchEvent.UnHit) ? 1 : 0, OPTIONS_SWITCH_TRIGGER_PLAYFIELD);
				if (EditorGUI.EndChangeCheck())
				{
					switchListData.Trigger = (index == 1) ? SwitchEvent.UnHit : SwitchEvent.Hit;
					updateAction(); 
				}
			}
		}

		private void RenderOff(SwitchListData switchListData, Rect cellRect, Action updateAction)
		{
			if (switchListData.Source == SwitchSource.InputSystem || switchListData.Source == SwitchSource.Playfield)
			{
				if (switchListData.Type == SwitchType.OnOff)
				{
					if (switchListData.Source == SwitchSource.InputSystem)
					{
						EditorGUI.BeginChangeCheck();
						int index = EditorGUI.Popup(cellRect, (switchListData.Trigger == SwitchEvent.KeyUp) ? 0 : 1, OPTIONS_SWITCH_TRIGGER_INPUT_SYSTEM);
						if (EditorGUI.EndChangeCheck())
						{
							switchListData.Trigger = (index == 0) ? SwitchEvent.KeyUp : SwitchEvent.KeyDown;
							updateAction();
						}
					}
					else if (switchListData.Source == SwitchSource.Playfield)
					{
						int index = EditorGUI.Popup(cellRect, (switchListData.Trigger == SwitchEvent.UnHit) ? 0 : 1, OPTIONS_SWITCH_TRIGGER_PLAYFIELD);
						if (EditorGUI.EndChangeCheck())
						{
							switchListData.Trigger = (index == 0) ? SwitchEvent.UnHit : SwitchEvent.Hit;
							updateAction();
						}
					}
				}
				else if (switchListData.Type == SwitchType.Pulse)
				{
					var labelRect = cellRect;
					labelRect.x = labelRect.x + labelRect.width - 20;
					labelRect.width = 20;

					var intFieldRect = cellRect;
					intFieldRect.width = intFieldRect.width - 25;

					EditorGUI.BeginChangeCheck();
					var pulse = EditorGUI.IntField(intFieldRect, switchListData.Pulse);
					if (EditorGUI.EndChangeCheck())
					{
						switchListData.Pulse = pulse;
						updateAction();
					}

					EditorGUI.LabelField(labelRect, "ms");
				}
			}
		}

		private Texture GetIcon(SwitchListData switchListData)
		{
			string image = null;

			if (switchListData.Source == SwitchSource.Playfield)
			{
				foreach (var item in _switchables)
				{
					if (item.Name == switchListData.Element)
					{
						if (item is BumperAuthoring)
						{
							image = "bumper"; 
						}
						else if (item is FlipperAuthoring)
						{
							image = "flipper";
						}
						else if (item is GateAuthoring)
						{
							image = "gate";
						}
						else if (item is HitTargetAuthoring)
						{
							image = "target";
						}
						else if (item is KickerAuthoring)
						{
							image = "kicker";
						}
						else if (item is PrimitiveAuthoring)
						{
							image = "primitive";
						}
						else if (item is RubberAuthoring)
						{
							image = "rubber";
						}
						else if (item is SurfaceAuthoring)
						{
							image = "surface";
						}
						else if (item is TriggerAuthoring)
						{
							image = "trigger";
						}
						else if (item is SpinnerAuthoring)
						{
							image = "spinner";
						}
						break;
					}
				}
			}
			else if (switchListData.Source == SwitchSource.Constant)
			{
				image = "switch_" +
					(switchListData.Constant == SwitchConstant.NC ? "nc" : "no");
			}

			if (image != null)
			{
				return AssetDatabase.LoadAssetAtPath<Texture2D>($"{ICON_PATH}/icon_" + image + ".png");
			}

			return null;
		}
	}
}