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

using System;
using System.Collections.Generic;
using NLog;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using Logger = NLog.Logger;
using Object = UnityEngine.Object;

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

		private InputManager _inputManager;
		private SwitchListViewItemRenderer _listViewItemRenderer;
		private bool _needsAssetRefresh;

		[MenuItem("Visual Pinball/Switch Manager", false, 301)]
		public static void ShowWindow()
		{
			GetWindow<SwitchManager>();
		}

		public override void OnEnable()
		{
			titleContent = new GUIContent("Switch Manager", Icons.Switch(false, IconSize.Small));

			RowHeight = 22;

			base.OnEnable();
		}

		private void OnFocus()
		{
			_inputManager = new InputManager(RESOURCE_PATH);
			_listViewItemRenderer = new SwitchListViewItemRenderer(_gleSwitches, TableComponent, _inputManager);
			_needsAssetRefresh = true;
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
				RecordUndo("Populate all switch mappings");
				TableComponent.MappingConfig.PopulateSwitches(GetAvailableEngineSwitches(), TableComponent);
				Reload();
			}

			if (GUILayout.Button("Remove All", GUILayout.ExpandWidth(false)))
			{
				if (EditorUtility.DisplayDialog("Switch Manager", "Are you sure want to remove all switch mappings?", "Yes", "Cancel")) {
					RecordUndo("Remove all switch mappings");
					TableComponent.MappingConfig.RemoveAllSwitches();
				}
				Reload();
			}
		}

		protected override void OnListViewItemRenderer(SwitchListData data, Rect cellRect, int column)
		{
			_listViewItemRenderer.Render(TableComponent, data, cellRect, column, switchListData => {
				RecordUndo(DataTypeName + " Data Change");

				switchListData.Update();
			});
		}

		#region Data management
		protected override List<SwitchListData> CollectData()
		{
			List<SwitchListData> data = new List<SwitchListData>();

			foreach (var mappingsSwitchData in TableComponent.MappingConfig.Switches)
			{
				data.Add(new SwitchListData(mappingsSwitchData));
			}

			RefreshSwitchIds();

			return data;
		}

		protected override void AddNewData(string undoName, string newName)
		{
			RecordUndo(undoName);

			TableComponent.MappingConfig.AddSwitch();
		}

		protected override void RemoveData(string undoName, SwitchListData data)
		{
			RecordUndo(undoName);

			TableComponent.MappingConfig.RemoveSwitch(data.SwitchMapping);
		}

		protected override void CloneData(string undoName, string newName, SwitchListData data)
		{
			RecordUndo(undoName);

			TableComponent.MappingConfig.AddSwitch(new SwitchMapping {
				Id = data.Id,
				InternalId = data.InternalId,
				IsNormallyClosed = data.NormallyClosed,
				Description = data.Description,
				Source = data.Source,
				InputActionMap = data.InputActionMap,
				InputAction = data.InputAction,
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

		private void RefreshSwitchIds()
		{
			_gleSwitches.Clear();
			_gleSwitches.AddRange(TableComponent.MappingConfig.GetSwitchIds(GetAvailableEngineSwitches()));
		}

		private GamelogicEngineSwitch[] GetAvailableEngineSwitches()
		{
			var gle = TableComponent.gameObject.GetComponent<IGamelogicEngine>();
			return gle == null ? Array.Empty<GamelogicEngineSwitch>() : gle.AvailableSwitches;
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
