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
using NLog;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT.Mappings;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Editor UI for VPE switches
	/// </summary>
	///
	internal class SwitchManager : ManagerWindow<SwitchListData>
	{
		private readonly string RESOURCE_PATH = "Assets/Resources";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override string DataTypeName => "Switch";

		protected override bool DetailsEnabled => false;
		protected override bool ListViewItemRendererEnabled => true;

		private readonly List<GamelogicEngineSwitch> _gleSwitches = new List<GamelogicEngineSwitch>();
		private readonly Dictionary<string, ISwitchAuthoring> _switches = new Dictionary<string, ISwitchAuthoring>();
		private readonly Dictionary<string, ISwitchDeviceAuthoring> _switchDevices = new Dictionary<string, ISwitchDeviceAuthoring>();

		private InputManager _inputManager;
		private SwitchListViewItemRenderer _listViewItemRenderer;
		private bool _needsAssetRefresh;

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

		public override void OnEnable()
		{
			titleContent = new GUIContent("Switch Manager",
				Icons.Switch(false, size: IconSize.Small));

			RowHeight = 22;

			base.OnEnable();
		}

		protected override void OnFocus()
		{
			_inputManager = new InputManager(RESOURCE_PATH);
			_listViewItemRenderer = new SwitchListViewItemRenderer(_gleSwitches, _switches, _switchDevices, _inputManager);
			_needsAssetRefresh = true;

			base.OnFocus();
		}

		protected override bool SetupCompleted()
		{
			if (_tableAuthoring == null)
			{
				DisplayMessage("No table set.");
				return false;
			}

			var gle = _tableAuthoring.gameObject.GetComponent<IGameEngineAuthoring>();

			if (gle == null)
			{
				DisplayMessage("No gamelogic engine set.");
				return false;
			}

			if (_needsAssetRefresh)
			{
				AssetDatabase.Refresh();
				_needsAssetRefresh = false;
			}

			return true;
		}

		protected override void OnButtonBarGUI()
		{
			if (GUILayout.Button("Populate All", GUILayout.ExpandWidth(false)))
			{
				if (_tableAuthoring != null)
				{
					RecordUndo("Populate all switch mappings");
					_tableAuthoring.Table.Mappings.PopulateSwitches(GetAvailableEngineSwitches(), _tableAuthoring.Table.Switchables, _tableAuthoring.Table.SwitchableDevices);
					Reload();
				}
			}

			if (GUILayout.Button("Remove All", GUILayout.ExpandWidth(false)))
			{
				if (_tableAuthoring != null)
				{
					if (EditorUtility.DisplayDialog("Switch Manager", "Are you sure want to remove all switch mappings?", "Yes", "Cancel")) {
						RecordUndo("Remove all switch mappings");
						_tableAuthoring.Mappings.RemoveAllSwitches();
					}
					Reload();
				}
			}
		}

		protected override void OnListViewItemRenderer(SwitchListData data, Rect cellRect, int column)
		{
			_listViewItemRenderer.Render(_tableAuthoring, data, cellRect, column, switchListData => {
				RecordUndo(DataTypeName + " Data Change");

				switchListData.Update();
			});
		}

		#region Data management
		protected override List<SwitchListData> CollectData()
		{
			List<SwitchListData> data = new List<SwitchListData>();

			foreach (var mappingsSwitchData in _tableAuthoring.Mappings.Switches)
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

			_tableAuthoring.Mappings.AddSwitch(new MappingsSwitchData());
		}

		protected override void RemoveData(string undoName, SwitchListData data)
		{
			RecordUndo(undoName);

			_tableAuthoring.Mappings.RemoveSwitch(data.MappingsSwitchData);
		}

		protected override void CloneData(string undoName, string newName, SwitchListData data)
		{
			RecordUndo(undoName);

			_tableAuthoring.Mappings.AddSwitch(new MappingsSwitchData
			{
				Id = data.Id,
				Description = data.Description,
				Source = data.Source,
				InputActionMap = data.InputActionMap,
				InputAction = data.InputAction,
				PlayfieldItem = data.PlayfieldItem,
				Constant = data.Constant,
				PulseDelay = data.PulseDelay
			});
		}
		#endregion

		#region Helper methods
		private void DisplayMessage(string message)
		{
			GUILayout.BeginHorizontal();
			GUILayout.BeginVertical();
			GUILayout.FlexibleSpace();
			var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
			EditorGUILayout.LabelField(message, style, GUILayout.ExpandWidth(true));
			GUILayout.FlexibleSpace();
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
		}

		private void RefreshSwitches()
		{
			_switches.Clear();
			_switchDevices.Clear();

			if (_tableAuthoring != null) {
				foreach (var item in _tableAuthoring.GetComponentsInChildren<ISwitchAuthoring>()) {
					_switches.Add(item.Name.ToLower(), item);
				}
				foreach (var item in _tableAuthoring.GetComponentsInChildren<ISwitchDeviceAuthoring>()) {
					_switchDevices.Add(item.Name.ToLower(), item);
				}
			}
		}

		private void RefreshSwitchIds()
		{
			_gleSwitches.Clear();

			_gleSwitches.AddRange(_tableAuthoring.Table.Mappings.GetSwitchIds(GetAvailableEngineSwitches()));
		}

		private GamelogicEngineSwitch[] GetAvailableEngineSwitches()
		{
			var gle = _tableAuthoring.gameObject.GetComponent<IGameEngineAuthoring>();
			return gle == null ? new GamelogicEngineSwitch[0] : ((IGamelogicEngineWithSwitches) gle.GameEngine).AvailableSwitches;
		}

		#endregion

		#region Undo Redo
		private void RestoreMappings()
		{
			if (_recordMappings == null) { return; }
			if (_tableAuthoring == null) { return; }
			if (_recordMappings.Table == _tableAuthoring)
			{
				_tableAuthoring.RestoreMappings(_recordMappings.Mappings);
			}
		}

		protected override void UndoPerformed()
		{
			RestoreMappings();
			base.UndoPerformed();
		}

		private void RecordUndo(string undoName)
		{
			if (_tableAuthoring == null) { return; }
			if (_recordMappings == null)
			{
				_recordMappings = CreateInstance<SerializedMappings>();
			}
			_recordMappings.Table = _tableAuthoring;
			_recordMappings.Mappings = _tableAuthoring.Mappings;

			Undo.RecordObjects(new Object[] { this, _recordMappings }, undoName);
		}
		#endregion
	}
}
