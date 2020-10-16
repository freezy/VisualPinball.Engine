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

		private readonly List<string> _ids = new List<string>();
		private readonly Dictionary<string, ICoilAuthoring> _coils = new Dictionary<string, ICoilAuthoring>();

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

		protected override void OnEnable()
		{
			titleContent = new GUIContent("Coil Manager");

			RowHeight = 22;

			base.OnEnable();
		}

		protected override void OnFocus()
		{
			_listViewItemRenderer = new CoilListViewItemRenderer(_ids, _coils);

			base.OnFocus();
		}

		protected override bool SetupCompleted()
		{
			if (_table == null)
			{
				DisplayMessage("No table set.");
				return false;
			}

			var gle = _table.gameObject.GetComponent<IGameEngineAuthoring>();

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
				if (_table != null)
				{
					RecordUndo("Populate all coil mappings");

					foreach (var id in _ids) {

						var coilMapping =
							_table.Mappings.Coils
							.FirstOrDefault(mappingsCoilData => mappingsCoilData.Id == id);

						if (coilMapping == null) {
							var matchKey = int.TryParse(id, out var numericCoilId)
								? $"sw{numericCoilId}"
								: id;

							var matchedItem = _coils.ContainsKey(matchKey)
								? _coils[matchKey]
								: null;

							_table.Mappings.AddCoil(new MappingsCoilData {
								Id = id
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
					if (EditorUtility.DisplayDialog("Coil Manager", "Are you sure want to remove all coil mappings?", "Yes", "Cancel")) {
						RecordUndo("Remove all coil mappings");
						_table.Mappings.RemoveAllCoils();
					}
					Reload();
				}
			}
		}

		protected override void OnListViewItemRenderer(CoilListData data, Rect cellRect, int column)
		{
			_listViewItemRenderer.Render(_table, data, cellRect, column, coilListData => {
				RecordUndo(DataTypeName + " Data Change");

				coilListData.Update();
			});
		}

		#region Data management
		protected override List<CoilListData> CollectData()
		{
			List<CoilListData> data = new List<CoilListData>();

			foreach (var mappingsCoilData in _table.Mappings.Coils)
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

			_table.Mappings.AddCoil(new MappingsCoilData());
		}

		protected override void RemoveData(string undoName, CoilListData data)
		{
			RecordUndo(undoName);

			_table.Mappings.RemoveCoil(data.MappingsCoilData);
		}

		protected override void CloneData(string undoName, string newName, CoilListData data)
		{
			RecordUndo(undoName);

			_table.Mappings.AddCoil(new MappingsCoilData
			{
				Id = data.Id,
				Description = data.Description,
				Destination = data.Destination,
				PlayfieldItem = data.PlayfieldItem,
				Device = data.Device,
				DeviceItem = data.DeviceItem,
				Type = data.Type,
				Pulse = data.Pulse
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

			if (_table != null)
			{
				foreach (var item in _table.GetComponentsInChildren<ICoilAuthoring>())
				{
					_coils.Add(item.Name.ToLower(), item);
				}
			}
		}

		private void RefreshCoilIds()
		{
			_ids.Clear();
			var gle = _table.gameObject.GetComponent<IGameEngineAuthoring>();
			if (gle != null) {
				_ids.AddRange(((IGamelogicEngineWithCoils)gle.GameEngine).AvailableCoils);

			} else {
				// todo show this in the editor window along with instructions.
				Logger.Warn("Either there is not game logic engine component on the table, or it doesn't support coils.");
			}

			foreach (var mappingsCoilData in _table.Mappings.Coils)
			{
				if (_ids.IndexOf(mappingsCoilData.Id) == -1)
				{
					_ids.Add(mappingsCoilData.Id);
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
