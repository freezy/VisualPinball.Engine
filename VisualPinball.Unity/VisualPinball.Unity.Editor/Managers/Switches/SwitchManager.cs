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

using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using VisualPinball.Engine.VPT.MappingConfig;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Editor UI for VPE switches
	/// </summary>
	///

	class SwitchManager : ManagerWindow<SwitchListData>
	{
		private readonly string RESOURCE_PATH = "Assets/Resources";

		protected override string DataTypeName => "Switch";

		protected override bool DetailsEnabled => false;
		protected override bool ListViewItemRendererEnabled => true;

		private List<string> _ids = new List<string>();
		private List<ISwitchableAuthoring> _switchables = new List<ISwitchableAuthoring>();
		private InputManager _inputManager;
		private SwitchListViewItemRenderer _listViewItemRenderer;

		private class SerializedMappingConfigs : ScriptableObject
		{
			public TableAuthoring Table;
			public List<MappingConfigData> MappingConfigs = new List<MappingConfigData>();
		}
		private SerializedMappingConfigs _recordMappingConfigs;

		[MenuItem("Visual Pinball/Switch Manager", false, 106)]
		public static void ShowWindow()
		{
			GetWindow<SwitchManager>();
		}

		protected override void OnEnable()
		{
			titleContent = new GUIContent("Switch Manager",
				Icons.Switch(false, color: IconColor.Gray, size: IconSize.Small));

			RowHeight = 22;

			_inputManager = new InputManager(RESOURCE_PATH);
			AssetDatabase.Refresh();

			_listViewItemRenderer = new SwitchListViewItemRenderer(_ids, _switchables, _inputManager);

			base.OnEnable();
		}

		protected override void OnButtonBarGUI()
		{
			if (GUILayout.Button("Populate All", GUILayout.ExpandWidth(false)))
			{
				if (_table != null)
				{
					RecordUndo("Populate All");

					var mappingConfigData = GetSwitchMappingConfig();

					FindNamedSwitchables((switchableItem, id) =>
					{
						if (GetSwitchMappingEntryByID(id) == null)
						{
							MappingEntryData entry = new MappingEntryData
							{
								ID = id,
								Source = SwitchSource.Playfield,
								PlayfieldItem = switchableItem.Name,
								Description = switchableItem.DefaultDescription,
								Type = (switchableItem is KickerAuthoring || switchableItem is TriggerAuthoring) ? SwitchType.OnOff : SwitchType.Pulse
							};

							mappingConfigData.MappingEntries =
								mappingConfigData.MappingEntries.Append(entry).ToArray();
						}
					});

					Reload();
				}
			}
		}

		protected override void OnListViewItemRenderer(SwitchListData data, Rect cellRect, int column)
		{
			_listViewItemRenderer.Render(data, cellRect, column, (switchListData) => {
				RecordUndo(DataTypeName + " Data Change");

				switchListData.Update();
			});
		}

		#region Data management
		protected override List<SwitchListData> CollectData()
		{
			List<SwitchListData> data = new List<SwitchListData>();

			var mappingConfigData = GetSwitchMappingConfig();

			foreach (var mappingEntryData in mappingConfigData.MappingEntries)
			{
				data.Add(new SwitchListData(mappingEntryData));
			}

			RefreshSwitchables();
			RefreshIDs();

			return data;
		}

		protected override void AddNewData(string undoName, string newName)
		{
			RecordUndo(undoName);

			var mappingConfigData = GetSwitchMappingConfig();

			mappingConfigData.MappingEntries =
				mappingConfigData.MappingEntries.Append(new MappingEntryData { ID = "" }).ToArray();
		}

		protected override void RemoveData(string undoName, SwitchListData data)
		{
			RecordUndo(undoName);

			var mappingConfigData = GetSwitchMappingConfig();

			mappingConfigData.MappingEntries =
				mappingConfigData.MappingEntries.Except(new[] { data.MappingEntryData }).ToArray();
		}

		protected override void CloneData(string undoName, string newName, SwitchListData data)
		{
			RecordUndo(undoName);

			var mappingConfigData = GetSwitchMappingConfig();

			mappingConfigData.MappingEntries =
				mappingConfigData.MappingEntries.Append(new MappingEntryData
				{
					ID = data.ID,
					Description = data.Description,
					Source = data.Source,
					InputActionMap = data.InputActionMap,
					InputAction = data.InputAction,
					PlayfieldItem = data.PlayfieldItem,
					Constant = data.Constant,
					Type = data.Type,
					Pulse = data.Pulse
				}).ToArray();
		}
		#endregion

		#region Helper methods
		private void RefreshSwitchables()
		{
			_switchables.Clear();

			if (_table != null)
			{
				foreach (var item in _table.GetComponentsInChildren<ISwitchableAuthoring>())
				{
					_switchables.Add(item);
				}

				_switchables.Sort((s1, s2) => s1.Name.CompareTo(s2.Name));
			}
		}

		private void RefreshIDs()
		{
			FindNamedSwitchables((item, id) =>
			{
				if (_ids.IndexOf(id) == -1)
				{
					_ids.Add(id);
				}
			});

			var mappingConfigData = GetSwitchMappingConfig();

			foreach (var mappingEntryData in mappingConfigData.MappingEntries)
			{
				if (_ids.IndexOf(mappingEntryData.ID) == -1)
				{
					_ids.Add(mappingEntryData.ID);
				}
			}

			_ids.Sort();
		}

		private void FindNamedSwitchables(Action<ISwitchableAuthoring, string> action)
		{
			foreach (var item in _switchables)
			{
				var match = new Regex(@"^(sw)(\d+)$").Match(item.Name);
				if (match.Success)
				{
					action(item, match.Groups[2].Value);
				}
			}
		}

		private MappingConfigData GetSwitchMappingConfig()
		{
			if (_table != null)
			{
				if (_table.MappingConfigs.Count == 0)
				{
					_table.MappingConfigs.Add(new MappingConfigData("Switch", new MappingEntryData[0]));
					_table.Item.Data.NumMappingConfigs = 1;
				}

				return _table.MappingConfigs[0];
			}

			return null;
		}

		private MappingEntryData GetSwitchMappingEntryByID(string id)
		{
			var mappingConfigData = GetSwitchMappingConfig();

			if (mappingConfigData != null)
			{
				foreach (var mappingEntryData in mappingConfigData.MappingEntries)
				{
					if (mappingEntryData.ID == id)
					{
						return mappingEntryData;
					}
				}
			}

			return null;
		}
		#endregion

		#region Undo Redo
		private void RestoreTableMappingConfigs()
		{
			if (_recordMappingConfigs == null) { return; }
			if (_table == null) { return; }
			if (_recordMappingConfigs.Table == _table)
			{
				_table.RestoreMappingConfigs(_recordMappingConfigs.MappingConfigs);
			}
		}

		protected override void UndoPerformed()
		{
			RestoreTableMappingConfigs();
			base.UndoPerformed();
		}

		private void RecordUndo(string undoName)
		{
			if (_table == null) { return; }
			if (_recordMappingConfigs == null)
			{
				_recordMappingConfigs = CreateInstance<SerializedMappingConfigs>();
			}
			_recordMappingConfigs.Table = _table;
			_recordMappingConfigs.MappingConfigs.Clear();
			_recordMappingConfigs.MappingConfigs.AddRange(_table?.MappingConfigs);

			Undo.RecordObjects(new UnityEngine.Object[] { this, _recordMappingConfigs }, undoName);
		}
		#endregion
	}
}
