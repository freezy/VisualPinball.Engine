﻿// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using Logger = NLog.Logger;
using Object = UnityEngine.Object;

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

		private CoilListViewItemRenderer _listViewItemRenderer;

		[MenuItem("Pinball/Coil Manager", false, 302)]
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

		private void OnFocus()
		{
			_listViewItemRenderer = new CoilListViewItemRenderer(TableComponent, _gleCoils);
		}

		protected override bool SetupCompleted()
		{
			if (TableComponent == null)
			{
				DisplayMessage("No table set.");
				return false;
			}

			var gle = TableComponent.gameObject.GetComponent<IGamelogicEngine>();

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
				RecordUndo("Populate all coil mappings");
				TableComponent.MappingConfig.PopulateCoils(GetAvailableEngineCoils(), TableComponent);
				Reload();
				LampManager.Refresh();
			}

			if (GUILayout.Button("Remove All", GUILayout.ExpandWidth(false)))
			{
				if (EditorUtility.DisplayDialog("Coil Manager", "Are you sure want to remove all coil mappings?", "Yes", "Cancel")) {
					RecordUndo("Remove all coil mappings");
					TableComponent.MappingConfig.RemoveAllCoils();
				}
				Reload();
				LampManager.Refresh();
			}
		}

		protected override void OnListViewItemRenderer(CoilListData data, Rect cellRect, int column)
		{
			_listViewItemRenderer.Render(TableComponent, data, cellRect, column, coilListData => {
				RecordUndo(DataTypeName + " Data Change");
				coilListData.Update();
			});
		}

		#region Data management
		protected override List<CoilListData> CollectData()
		{
			List<CoilListData> data = new List<CoilListData>();
			foreach (var coilMapping in TableComponent.MappingConfig.Coils) {
				data.Add(new CoilListData(coilMapping));
			}

			RefreshCoilIds();

			return data;
		}

		protected override void AddNewData(string undoName, string newName)
		{
			RecordUndo(undoName);
			TableComponent.MappingConfig.AddCoil(new CoilMapping());
		}

		protected override void RemoveData(string undoName, CoilListData data)
		{
			RecordUndo(undoName);
			TableComponent.MappingConfig.RemoveCoil(data.CoilMapping);

			// todo if it's a lamp, also delete the lamp entry.
			if (data.CoilMapping.Destination == CoilDestination.Lamp) {
				var lampEntry = TableComponent.MappingConfig.Lamps.FirstOrDefault(l => l.Id == data.Id && l.IsCoil);
				if (lampEntry != null) {
					TableComponent.MappingConfig.RemoveLamp(lampEntry);
					LampManager.Refresh();
				}
			}
		}

		protected override void CloneData(string undoName, string newName, CoilListData data)
		{
			RecordUndo(undoName);
			TableComponent.MappingConfig.AddCoil(new CoilMapping {
				Id = data.Id,
				Description = data.Description,
				Destination = data.Destination,
				Device = data.Device,
				DeviceItem = data.DeviceItem,
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

		private void RefreshCoilIds()
		{
			_gleCoils.Clear();
			_gleCoils.AddRange(TableComponent.MappingConfig.GetCoils(GetAvailableEngineCoils()));
		}

		private GamelogicEngineCoil[] GetAvailableEngineCoils()
		{
			var gle = TableComponent.gameObject.GetComponent<IGamelogicEngine>();
			return gle == null ? Array.Empty<GamelogicEngineCoil>() : gle.RequestedCoils;
		}

		#endregion

		#region Undo Redo

		private void RecordUndo(string undoName)
		{
			Undo.RecordObjects(new Object[] { this, TableComponent }, undoName);
		}

		#endregion
	}
}
