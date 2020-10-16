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
using VisualPinball.Engine.VPT.Mappings;
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
		private readonly Dictionary<string, ISwitchAuthoring> _switches = new Dictionary<string, ISwitchAuthoring>();

		private InputManager _inputManager;
		private SwitchListViewItemRenderer _listViewItemRenderer;

		private class SerializedMappings : ScriptableObject
		{
			public TableAuthoring Table;
			public MappingsData Mappings;
		}
		private SerializedMappings _recordMappings;

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

			_listViewItemRenderer = new SwitchListViewItemRenderer(_ids, _switches, _inputManager);

			base.OnEnable();
		}

		protected override bool SetupCompleted()
		{
			if (_table == null) {
				return true;
			}

			var gle = _table.gameObject.GetComponent<DefaultGameEngineAuthoring>();
			if (gle != null) {
				return true;
			}

			// show error centered
			GUILayout.BeginHorizontal();
			GUILayout.BeginVertical();
			GUILayout.FlexibleSpace();
			var style = new GUIStyle(GUI.skin.label) {alignment = TextAnchor.MiddleCenter};
			EditorGUILayout.LabelField("No gamelogic engine set.", style, GUILayout.ExpandWidth(true));
			GUILayout.FlexibleSpace();
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();

			return false;
		}

		protected override void OnButtonBarGUI()
		{
			if (GUILayout.Button("Populate All", GUILayout.ExpandWidth(false)))
			{
				if (_table != null)
				{
					RecordUndo("Populate all switch mappings");

					foreach (var id in _ids)
					{
						var switchMapping =		
							_table.Mappings.Switches
							.FirstOrDefault(mappingsSwitchData => mappingsSwitchData.Id == id);

						if (switchMapping == null) {
							var matchKey = int.TryParse(id, out var numericSwitchId)
								? $"sw{numericSwitchId}"
								: id;

							var matchedItem = _switches.ContainsKey(matchKey)
								? _switches[matchKey]
								: null;

							var source = GuessSource(id);

							_table.Mappings.AddSwitch(new MappingsSwitchData {
								Id = id,
								Source = source,
								PlayfieldItem = matchedItem == null ? string.Empty : matchedItem.Name,
								Type = matchedItem is KickerAuthoring || matchedItem is TriggerAuthoring || source == SwitchSource.InputSystem
									? SwitchType.OnOff
									: SwitchType.Pulse,
								InputActionMap = GuessInputMap(id),
								InputAction = source == SwitchSource.InputSystem ? GuessInputAction(id) : null,
							});
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
						_table.Mappings.RemoveAllSwitches();
					}
					Reload();
				}
			}
		}

		private int GuessSource(string switchId)
		{
			if (switchId.Contains("left_flipper")) {
				return SwitchSource.InputSystem;
			}
			if (switchId.Contains("right_flipper")) {
				return SwitchSource.InputSystem;
			}
			if (switchId.Contains("create_ball")) {
				return SwitchSource.InputSystem;
			}

			return SwitchSource.Playfield;
		}

		private string GuessInputMap(string switchId)
		{
			if (switchId.Contains("create_ball")) {
				return InputManager.MapDebug;
			}
			return InputManager.MapCabinetSwitches;
		}

		private string GuessInputAction(string switchId)
		{
			if (switchId.Contains("left_flipper")) {
				return InputManager.ActionLeftFlipper;
			}
			if (switchId.Contains("right_flipper")) {
				return InputManager.ActionRightFlipper;
			}
			if (switchId.Contains("create_ball")) {
				return InputManager.ActionCreateBall;
			}

			return string.Empty;
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

			foreach (var mappingsSwitchData in _table.Mappings.Switches)
			{
				data.Add(new SwitchListData(mappingsSwitchData));
			}

			RefreshSwitches();
			RefreshSwitchIds();

			return data;
		}

		protected override void AddNewData(string undoName, string newName)
		{
			RecordUndo(undoName);

			_table.Mappings.AddSwitch(new MappingsSwitchData());
		}

		protected override void RemoveData(string undoName, SwitchListData data)
		{
			RecordUndo(undoName);

			_table.Mappings.RemoveSwitch(data.MappingsSwitchData);
		}

		protected override void CloneData(string undoName, string newName, SwitchListData data)
		{
			RecordUndo(undoName);

			_table.Mappings.AddSwitch(new MappingsSwitchData
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
			});
		}
		#endregion

		#region Helper methods
		private void RefreshSwitches()
		{
			_switches.Clear();

			if (_table != null)
			{
				foreach (var item in _table.GetComponentsInChildren<ISwitchAuthoring>())
				{
					_switches.Add(item.Name.ToLower(), item);
				}
			}
		}

		private void RefreshSwitchIds()
		{
			_ids.Clear();
			var gle = _table.gameObject.GetComponent<IGameEngineAuthoring>();
			if (gle != null) {
				_ids.AddRange(((IGamelogicEngineWithSwitches)gle.GameEngine).AvailableSwitches);

			} else {
				// todo show this in the editor window along with instructions.
				Logger.Warn("Either there is not game logic engine component on the table, or it doesn't support switches.");
			}

			foreach (var mappingsSwitchData in _table.Mappings.Switches)
			{
				if (_ids.IndexOf(mappingsSwitchData.Id) == -1)
				{
					_ids.Add(mappingsSwitchData.Id);
				}
			}

			_ids.Sort();
		}
		#endregion

		#region Undo Redo
		private void RestoreMappings()
		{
			if (_recordMappings == null) { return; }
			if (_table == null) { return; }
			if (_recordMappings.Table == _table)
			{
				_table.RestoreMappings(_recordMappings.Mappings);
			}
		}

		protected override void UndoPerformed()
		{
			RestoreMappings();
			base.UndoPerformed();
		}

		private void RecordUndo(string undoName)
		{
			if (_table == null) { return; }
			if (_recordMappings == null)
			{
				_recordMappings = CreateInstance<SerializedMappings>();
			}
			_recordMappings.Table = _table;
			_recordMappings.Mappings = _table.Mappings;
			
			Undo.RecordObjects(new Object[] { this, _recordMappings }, undoName);
		}
		#endregion
	}
}
