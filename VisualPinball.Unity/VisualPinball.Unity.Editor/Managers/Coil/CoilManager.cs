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
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Mappings;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Editor UI for VPE coils
	/// </summary>
	///

	class CoilManager : ManagerWindow<CoilListData>
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override string DataTypeName => "Coil";

		protected override bool DetailsEnabled => false;
		protected override bool ListViewItemRendererEnabled => true;

		private readonly List<GamelogicEngineCoil> _gleCoils = new List<GamelogicEngineCoil>();
		private readonly Dictionary<string, ICoilAuthoring> _coils = new Dictionary<string, ICoilAuthoring>();
		private readonly Dictionary<string, ICoilDeviceAuthoring> _coilDevices = new Dictionary<string, ICoilDeviceAuthoring>();

		private CoilListViewItemRenderer _listViewItemRenderer;

		private class SerializedMappings : ScriptableObject
		{
			public TableAuthoring Table;
			public MappingsData Mappings;
		}
		private SerializedMappings _recordMappings;

		[MenuItem("Visual Pinball/Coil Manager", false, 107)]
		public static void ShowWindow()
		{
			GetWindow<CoilManager>();
		}

		public override void OnEnable()
		{
			titleContent = new GUIContent("Coil Manager", Icons.Coil(IconSize.Small));
			RowHeight = 22;

			base.OnEnable();
		}

		protected override void OnFocus()
		{
			_listViewItemRenderer = new CoilListViewItemRenderer(_gleCoils, _coils, _coilDevices);

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

			return true;
		}

		protected override void OnButtonBarGUI()
		{
			if (GUILayout.Button("Populate All", GUILayout.ExpandWidth(false)))
			{
				if (_tableAuthoring != null)
				{
					RecordUndo("Populate all coil mappings");
					_tableAuthoring.Table.Mappings.PopulateCoils(GetAvailableEngineCoils(), _tableAuthoring.Table.Coilables, _tableAuthoring.Table.CoilableDevices);
					Reload();
				}
			}

			if (GUILayout.Button("Remove All", GUILayout.ExpandWidth(false)))
			{
				if (_tableAuthoring != null)
				{
					if (EditorUtility.DisplayDialog("Coil Manager", "Are you sure want to remove all coil mappings?", "Yes", "Cancel")) {
						RecordUndo("Remove all coil mappings");
						_tableAuthoring.Mappings.RemoveAllCoils();
					}
					Reload();
				}
			}
		}

		protected override void OnListViewItemRenderer(CoilListData data, Rect cellRect, int column)
		{
			_listViewItemRenderer.Render(_tableAuthoring, data, cellRect, column, coilListData => {
				RecordUndo(DataTypeName + " Data Change");

				coilListData.Update();
			});
		}

		#region Data management
		protected override List<CoilListData> CollectData()
		{
			List<CoilListData> data = new List<CoilListData>();

			foreach (var mappingsCoilData in _tableAuthoring.Mappings.Coils)
			{
				data.Add(new CoilListData(mappingsCoilData));
			}

			RefreshCoils();
			RefreshCoilIds();

			return data;
		}

		protected override void AddNewData(string undoName, string newName)
		{
			RecordUndo(undoName);

			_tableAuthoring.Mappings.AddCoil(new MappingsCoilData());
		}

		protected override void RemoveData(string undoName, CoilListData data)
		{
			RecordUndo(undoName);

			_tableAuthoring.Mappings.RemoveCoil(data.MappingsCoilData);
		}

		protected override void CloneData(string undoName, string newName, CoilListData data)
		{
			RecordUndo(undoName);

			_tableAuthoring.Mappings.AddCoil(new MappingsCoilData
			{
				Id = data.Id,
				Description = data.Description,
				Destination = data.Destination,
				PlayfieldItem = data.PlayfieldItem,
				Device = data.Device,
				DeviceItem = data.DeviceItem,
				Type = data.Type,
				HoldCoilId = data.HoldCoilId
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

		private void RefreshCoils()
		{
			_coils.Clear();
			_coilDevices.Clear();

			if (_tableAuthoring != null) {

				foreach (var item in _tableAuthoring.GetComponentsInChildren<ICoilAuthoring>()) {
					_coils.Add(item.Name.ToLower(), item);
				}

				foreach (var item in _tableAuthoring.GetComponentsInChildren<ICoilDeviceAuthoring>()) {
					_coilDevices.Add(item.Name.ToLower(), item);
				}
			}
		}

		private void RefreshCoilIds()
		{
			_gleCoils.Clear();
			_gleCoils.AddRange(_tableAuthoring.Table.Mappings.GetCoils(GetAvailableEngineCoils()));
		}

		private GamelogicEngineCoil[] GetAvailableEngineCoils()
		{
			var gle = _tableAuthoring.gameObject.GetComponent<IGameEngineAuthoring>();
			return gle == null ? new GamelogicEngineCoil[0] : ((IGamelogicEngineWithCoils) gle.GameEngine).AvailableCoils;
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
