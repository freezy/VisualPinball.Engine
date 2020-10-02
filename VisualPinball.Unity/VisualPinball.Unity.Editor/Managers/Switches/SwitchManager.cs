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

using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.MappingConfig;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Editor UI for VPE switches
	/// </summary>
	///

	class SwitchManager : ManagerWindow<SwitchListData>
	{
		private readonly string RESOURCE_PATH = "Assets/Resources";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override string DataTypeName => "Switch";

		protected override bool DetailsEnabled => false;
		protected override bool ListViewItemRendererEnabled => true;

		private readonly List<string> _ids = new List<string>();
		private readonly Dictionary<string, ISwitchableAuthoring> _switchables = new Dictionary<string, ISwitchableAuthoring>();

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
				Icons.Switch(false, size: IconSize.Small));

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
					RecordUndo("Populate all switch mappings");

					var mappingConfigData = GetSwitchMappingConfig();

					foreach (var switchId in _ids) {

						if (GetSwitchMappingEntryByID(switchId) == null) {

							var matchKey = int.TryParse(switchId, out var numericSwitchId)
								? $"sw{numericSwitchId}"
								: switchId;

							var matchedItem = _switchables.ContainsKey(matchKey)
								? _switchables[matchKey]
								: null;

							var entry = new MappingEntryData {
								Id = switchId,
								Source = SwitchSource.Playfield,
								PlayfieldItem = matchedItem == null ? string.Empty : matchedItem.Name,
								Type = matchedItem is KickerAuthoring || matchedItem is TriggerAuthoring
									? SwitchType.OnOff
									: SwitchType.Pulse
							};

							mappingConfigData.MappingEntries = mappingConfigData.MappingEntries.Append(entry).ToArray();
						}
					}
					Reload();
				}
			}

			if (GUILayout.Button("Remove All", GUILayout.ExpandWidth(false)))
			{
				if (_table != null)
				{
					if (EditorUtility.DisplayDialog("Switch Manager", "Are you sure want to remove all switch mappings?", "Yes", "Cancel")) {
						RecordUndo("Remove all switch mappings");
						var mappingConfigData = GetSwitchMappingConfig();
						mappingConfigData.MappingEntries = new MappingEntryData[0];
					}
					Reload();
				}
			}
		}

		protected override void OnListViewItemRenderer(SwitchListData data, Rect cellRect, int column)
		{
			_listViewItemRenderer.Render(_table, data, cellRect, column, switchListData => {
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
				mappingConfigData.MappingEntries.Append(new MappingEntryData { Id = "" }).ToArray();
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
					Id = data.Id,
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
					_switchables.Add(item.Name.ToLower(), item);
				}
			}
		}

		private void RefreshIDs()
		{
			_ids.Clear();
			var gle = _table.gameObject.GetComponent<DefaultGameEngineAuthoring>();
			if (gle != null) {
				_ids.AddRange(gle.GameEngine.AvailableSwitches);

			} else {
				// todo show this in the editor window along with instructions.
				Logger.Warn("Either there is not game logic engine component on the table, or it doesn't support switches.");
			}

			var mappingConfigData = GetSwitchMappingConfig();

			foreach (var mappingEntryData in mappingConfigData.MappingEntries)
			{
				if (_ids.IndexOf(mappingEntryData.Id) == -1)
				{
					_ids.Add(mappingEntryData.Id);
				}
			}

			_ids.Sort();
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
			return mappingConfigData?
				.MappingEntries
				.FirstOrDefault(mappingEntryData => mappingEntryData.Id == id);
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

			Undo.RecordObjects(new Object[] { this, _recordMappingConfigs }, undoName);
		}
		#endregion
	}
}
