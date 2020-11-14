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
using VisualPinball.Engine.VPT.Mappings;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Editor UI for VPE wirees
	/// </summary>
	///

	class WireManager : ManagerWindow<WireListData>
	{
		private readonly string RESOURCE_PATH = "Assets/Resources";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override string DataTypeName => "Wire";

		protected override bool DetailsEnabled => false;
		protected override bool ListViewItemRendererEnabled => true;

		private readonly Dictionary<string, ISwitchAuthoring> _switches = new Dictionary<string, ISwitchAuthoring>();
		private readonly Dictionary<string, ISwitchDeviceAuthoring> _switchDevices = new Dictionary<string, ISwitchDeviceAuthoring>();

		private readonly Dictionary<string, ICoilAuthoring> _coils = new Dictionary<string, ICoilAuthoring>();
		private readonly Dictionary<string, ICoilDeviceAuthoring> _coilDevices = new Dictionary<string, ICoilDeviceAuthoring>();

		private InputManager _inputManager;
		private bool _needsAssetRefresh;

		private WireListViewItemRenderer _listViewItemRenderer;

		private class SerializedMappings : ScriptableObject
		{
			public TableAuthoring Table;
			public MappingsData Mappings;
		}
		private SerializedMappings _recordMappings;

		[MenuItem("Visual Pinball/Wire Manager", false, 108)]
		public static void ShowWindow()
		{
			GetWindow<WireManager>();
		}

		public override void OnEnable()
		{
			titleContent = new GUIContent("Wire Manager");

			RowHeight = 22;

			base.OnEnable();
		}

		protected override void OnFocus()
		{
			_inputManager = new InputManager(RESOURCE_PATH);
			_listViewItemRenderer = new WireListViewItemRenderer(_switches, _switchDevices, _inputManager, _coils, _coilDevices);
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
			if (GUILayout.Button("Remove All", GUILayout.ExpandWidth(false)))
			{
				if (_tableAuthoring != null)
				{
					if (EditorUtility.DisplayDialog("Wire Manager", "Are you sure want to remove all wire mappings?", "Yes", "Cancel")) {
						RecordUndo("Remove all wire mappings");
						_tableAuthoring.Mappings.RemoveAllWires();
					}
					Reload();
				}
			}
		}

		protected override void OnListViewItemRenderer(WireListData data, Rect cellRect, int column)
		{
			_listViewItemRenderer.Render(_tableAuthoring, data, cellRect, column, wireListData => {
				RecordUndo(DataTypeName + " Data Change");

				wireListData.Update();
			});
		}

		#region Data management
		protected override List<WireListData> CollectData()
		{
			List<WireListData> data = new List<WireListData>();

			foreach (var mappingsWireData in _tableAuthoring.Mappings.Wires)
			{
				data.Add(new WireListData(mappingsWireData));
			}

			RefreshSwitches();
			RefreshCoils();

			return data;
		}

		protected override void AddNewData(string undoName, string newName)
		{
			RecordUndo(undoName);

			_tableAuthoring.Mappings.AddWire(new MappingsWireData());
		}

		protected override void RemoveData(string undoName, WireListData data)
		{
			RecordUndo(undoName);

			_tableAuthoring.Mappings.RemoveWire(data.MappingsWireData);
		}

		protected override void CloneData(string undoName, string newName, WireListData data)
		{
			RecordUndo(undoName);

			_tableAuthoring.Mappings.AddWire(new MappingsWireData
			{
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

			if (_tableAuthoring != null)
			{
				foreach (var item in _tableAuthoring.GetComponentsInChildren<ISwitchAuthoring>())
				{
					_switches.Add(item.Name.ToLower(), item);
				}
				foreach (var item in _tableAuthoring.GetComponentsInChildren<ISwitchDeviceAuthoring>())
				{
					_switchDevices.Add(item.Name.ToLower(), item);
				}
			}
		}

		private void RefreshCoils()
		{
			_coils.Clear();
			_coilDevices.Clear();

			if (_tableAuthoring != null)
			{

				foreach (var item in _tableAuthoring.GetComponentsInChildren<ICoilAuthoring>())
				{
					_coils.Add(item.Name.ToLower(), item);
				}

				foreach (var item in _tableAuthoring.GetComponentsInChildren<ICoilDeviceAuthoring>())
				{
					_coilDevices.Add(item.Name.ToLower(), item);
				}
			}
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
