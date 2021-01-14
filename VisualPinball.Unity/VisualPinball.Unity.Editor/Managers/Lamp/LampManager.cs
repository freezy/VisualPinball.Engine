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
using System.Linq;
using NLog;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT.Mappings;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Editor UI for VPE lamps
	/// </summary>
	///
	class LampManager : ManagerWindow<LampListData>
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override string DataTypeName => "Lamp";

		protected override bool DetailsEnabled => false;
		protected override bool ListViewItemRendererEnabled => true;

		private readonly List<GamelogicEngineLamp> _gleLamps = new List<GamelogicEngineLamp>();
		private readonly Dictionary<string, ILampAuthoring> _lamps = new Dictionary<string, ILampAuthoring>();

		private LampListViewItemRenderer _listViewItemRenderer;

		private class SerializedMappings : ScriptableObject
		{
			public TableAuthoring Table;
			public MappingsData Mappings;
		}
		private SerializedMappings _recordMappings;

		[MenuItem("Visual Pinball/Lamp Manager", false, 304)]
		public static void ShowWindow()
		{
			GetWindow<LampManager>();
		}

		public override void OnEnable()
		{
			titleContent = new GUIContent("Lamp Manager", Icons.Light(IconSize.Small));
			RowHeight = 22;

			base.OnEnable();
		}

		private void OnFocus()
		{
			_listViewItemRenderer = new LampListViewItemRenderer(_gleLamps, _lamps);
		}

		protected override bool SetupCompleted()
		{
			if (_tableAuthoring == null) {
				DisplayMessage("No table set.");
				return false;
			}

			var gle = _tableAuthoring.gameObject.GetComponent<IGameEngineAuthoring>();

			if (gle == null) {
				DisplayMessage("No gamelogic engine set.");
				return false;
			}

			return true;
		}

		protected override void OnButtonBarGUI()
		{
			if (GUILayout.Button("Populate All", GUILayout.ExpandWidth(false))) {
				if (_tableAuthoring != null) {
					RecordUndo("Populate all lamp mappings");
					_tableAuthoring.Table.Mappings.PopulateLamps(GetAvailableEngineLamps(), _tableAuthoring.Table.Lightables);
					Reload();
				}
			}

			if (GUILayout.Button("Remove All", GUILayout.ExpandWidth(false))) {
				if (_tableAuthoring != null) {
					if (EditorUtility.DisplayDialog("Lamp Manager", "Are you sure want to remove all lamp mappings?", "Yes", "Cancel")) {
						RecordUndo("Remove all lamp mappings");
						_tableAuthoring.Mappings.RemoveAllLamps();
					}
					Reload();
				}
			}
		}

		protected override void OnListViewItemRenderer(LampListData data, Rect cellRect, int column)
		{
			_listViewItemRenderer.Render(_tableAuthoring, data, cellRect, column, lampListData => {
				RecordUndo(DataTypeName + " Data Change");

				lampListData.Update();

				var lamp = _tableAuthoring.Table.Lightables.FirstOrDefault(c => c.Name == lampListData.PlayfieldItem);
			});
		}

		#region Data management
		protected override List<LampListData> CollectData()
		{
			List<LampListData> data = new List<LampListData>();

			foreach (var mappingsLampData in _tableAuthoring.Mappings.Lamps) {
				data.Add(new LampListData(mappingsLampData));
			}

			RefreshLamps();
			RefreshLampIds();

			return data;
		}

		protected override void AddNewData(string undoName, string newName)
		{
			RecordUndo(undoName);
			_tableAuthoring.Mappings.AddLamp(new MappingsLampData());
		}

		protected override void RemoveData(string undoName, LampListData data)
		{
			RecordUndo(undoName);
			_tableAuthoring.Mappings.RemoveLamp(data.MappingsLampData);
		}

		protected override void CloneData(string undoName, string newName, LampListData data)
		{
			RecordUndo(undoName);

			_tableAuthoring.Mappings.AddLamp(new MappingsLampData
			{
				Id = data.Id,
				Description = data.Description,
				Destination = data.Destination,
				PlayfieldItem = data.PlayfieldItem,
				Device = data.Device,
				DeviceItem = data.DeviceItem,
				Type = data.Type,
				Blue = data.Blue,
				Green = data.Green,
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

		private void RefreshLamps()
		{
			_lamps.Clear();

			if (_tableAuthoring != null) {
				foreach (var item in _tableAuthoring.GetComponentsInChildren<ILampAuthoring>()) {
					_lamps.Add(item.Name.ToLower(), item);
				}
			}
		}

		private void RefreshLampIds()
		{
			_gleLamps.Clear();
			_gleLamps.AddRange(_tableAuthoring.Table.Mappings.GetLamps(GetAvailableEngineLamps()));
		}

		private GamelogicEngineLamp[] GetAvailableEngineLamps()
		{
			var gle = _tableAuthoring.gameObject.GetComponent<IGameEngineAuthoring>();
			return gle == null ? new GamelogicEngineLamp[0] : ((IGamelogicEngineWithLamps)gle.GameEngine).AvailableLamps;
		}

		#endregion

		#region Undo Redo
		private void RestoreMappings()
		{
			if (_recordMappings == null) { return; }
			if (_tableAuthoring == null) { return; }
			if (_recordMappings.Table == _tableAuthoring) {
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
			if (_recordMappings == null) {
				_recordMappings = CreateInstance<SerializedMappings>();
			}
			_recordMappings.Table = _tableAuthoring;
			_recordMappings.Mappings = _tableAuthoring.Mappings;

			Undo.RecordObjects(new Object[] { this, _recordMappings }, undoName);
		}
		#endregion
	}
}
