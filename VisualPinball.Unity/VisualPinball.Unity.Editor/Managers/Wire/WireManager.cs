// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Editor UI for VPE wires
	/// </summary>
	///

	class WireManager : ManagerWindow<WireListData>
	{
		private readonly string RESOURCE_PATH = "Assets/Resources";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override string DataTypeName => "Wire";

		protected override bool DetailsEnabled => false;
		protected override bool ListViewItemRendererEnabled => true;

		private InputManager _inputManager;
		private bool _needsAssetRefresh;

		private WireListViewItemRenderer _listViewItemRenderer;

		[MenuItem("Visual Pinball/Wire Manager", false, 303)]
		public static void ShowWindow()
		{
			GetWindow<WireManager>();
		}

		public override void OnEnable()
		{
			titleContent = new GUIContent("Wire Manager", Icons.Plug(IconSize.Small));

			RowHeight = 22;

			base.OnEnable();
		}

		private void OnFocus()
		{
			_inputManager = new InputManager(RESOURCE_PATH);
			_listViewItemRenderer = new WireListViewItemRenderer(_tableAuthoring, _inputManager);
			_needsAssetRefresh = true;
		}

		protected override bool SetupCompleted()
		{
			if (_tableAuthoring == null)
			{
				DisplayMessage("No table set.");
				return false;
			}

			var gle = _tableAuthoring.gameObject.GetComponent<IGamelogicEngine>();

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
				if (EditorUtility.DisplayDialog("Wire Manager", "Are you sure want to remove all wire mappings?", "Yes", "Cancel")) {
					RecordUndo("Remove all wire mappings");
					_tableAuthoring.MappingConfig.RemoveAllWires();
				}
				Reload();
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
			var data = new List<WireListData>();
			foreach (var mappingsWireData in _tableAuthoring.MappingConfig.Wires) {
				data.Add(new WireListData(mappingsWireData));
			}
			return data;
		}

		protected override void AddNewData(string undoName, string newName)
		{
			RecordUndo(undoName);

			_tableAuthoring.MappingConfig.AddWire(new WireMapping());
		}

		protected override void RemoveData(string undoName, WireListData data)
		{
			RecordUndo(undoName);

			_tableAuthoring.MappingConfig.RemoveWire(data.WireMapping);
		}

		protected override void CloneData(string undoName, string newName, WireListData data)
		{
			RecordUndo(undoName);

			_tableAuthoring.MappingConfig.AddWire(new WireMapping());
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

		#endregion

		#region Undo Redo
		private void RestoreMappings()
		{
			// if (_recordMappings == null) { return; }
			// if (_tableAuthoring == null) { return; }
			// if (_recordMappings.Table == _tableAuthoring)
			// {
			// 	_tableAuthoring.RestoreMappings(_recordMappings.Mappings);
			// }
		}

		protected override void UndoPerformed()
		{
			RestoreMappings();
			base.UndoPerformed();
		}

		private void RecordUndo(string undoName)
		{
			// if (_tableAuthoring == null) { return; }
			// if (_recordMappings == null)
			// {
			// 	_recordMappings = CreateInstance<SerializedMappings>();
			// }
			// _recordMappings.Table = _tableAuthoring;
			// _recordMappings.Mappings = _tableAuthoring.Mappings;
			//
			// Undo.RecordObjects(new Object[] { this, _recordMappings }, undoName);
		}
		#endregion
	}
}
