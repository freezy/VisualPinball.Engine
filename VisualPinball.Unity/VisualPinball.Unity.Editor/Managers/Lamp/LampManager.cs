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
	/// Editor UI for VPE lamps
	/// </summary>
	///
	internal class LampManager : ManagerWindow<LampListData>
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override string DataTypeName => "Lamp";

		protected override bool DetailsEnabled => false;
		protected override bool ListViewItemRendererEnabled => true;

		private readonly List<GamelogicEngineLamp> _gleLamps = new List<GamelogicEngineLamp>();

		private LampListViewItemRenderer _listViewItemRenderer;

		private ToggleAction _toggleAction = ToggleAction.All;

		public static void Refresh()
		{
			if (HasOpenInstances<LampManager>()) {
				var f = focusedWindow;
				GetWindow<LampManager>().Reload();
				FocusWindowIfItsOpen(f.GetType());
			}
		}

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
			_listViewItemRenderer = new LampListViewItemRenderer(_gleLamps, TableComponent);
		}

		protected override bool SetupCompleted()
		{
			if (TableComponent == null) {
				DisplayMessage("No table set.");
				return false;
			}

			var gle = TableComponent.gameObject.GetComponent<IGamelogicEngine>();

			if (gle == null) {
				DisplayMessage("No gamelogic engine set.");
				return false;
			}

			return true;
		}

		protected override void OnButtonBarGUI()
		{
			if (GUILayout.Button("Populate All", GUILayout.ExpandWidth(false))) {
				if (TableComponent != null) {
					RecordUndo("Populate all lamp mappings");
					TableComponent.MappingConfig.PopulateLamps(GetAvailableEngineLamps(), TableComponent);
					Reload();
				}
			}

			if (GUILayout.Button("Remove All", GUILayout.ExpandWidth(false))) {
				if (TableComponent != null) {
					if (EditorUtility.DisplayDialog("Lamp Manager", "Are you sure want to remove all lamp mappings?", "Yes", "Cancel")) {
						RecordUndo("Remove all lamp mappings");
						TableComponent.MappingConfig.RemoveAllLamps();
					}
					Reload();
				}
			}

			GUILayout.FlexibleSpace();

			_toggleAction = (ToggleAction)EditorGUILayout.EnumPopup(_toggleAction);
			if (GUILayout.Button("Turn On", GUILayout.ExpandWidth(false))) {
				Toggle(l => l.enabled = true);
			}
			if (GUILayout.Button("Turn Off", GUILayout.ExpandWidth(false))) {
				Toggle(l => l.enabled = false);
			}
			if (GUILayout.Button("Select", GUILayout.ExpandWidth(false))) {
				var lights = new List<Light>();
				Toggle(lights.Add);
				Selection.objects = lights.Select(l => l.gameObject as Object).ToArray();
			}
		}

		private void Toggle(Action<Light> action)
		{
			if (TableComponent != null) {
				IEnumerable<LampMapping> selection = _toggleAction switch {
					ToggleAction.All => TableComponent.MappingConfig.Lamps,
					ToggleAction.Inserts => TableComponent.MappingConfig.Lamps.Where(lm => !lm.IsCoil && lm.Source == LampSource.Lamp),
					ToggleAction.GI => TableComponent.MappingConfig.Lamps.Where(lm => lm.Source == LampSource.GI),
					ToggleAction.Flasher => TableComponent.MappingConfig.Lamps.Where(lm => lm.IsCoil),
					_ => throw new ArgumentOutOfRangeException()
				};

				foreach (var lampMapping in selection) {
					if (lampMapping.Device == null) {
						continue;
					}

					var lights = lampMapping.Device is LightGroupComponent lightGroupComponent
						? lightGroupComponent.Lights.SelectMany(l => l.GetComponentsInChildren<Light>())
						: lampMapping.Device.gameObject.GetComponentsInChildren<Light>();

					foreach (var light in lights) {
						action(light);
					}
				}
			}
		}

		protected override void OnListViewItemRenderer(LampListData data, Rect cellRect, int column)
		{
			_listViewItemRenderer.Render(TableComponent, data, cellRect, column, lampListData => {
				RecordUndo(DataTypeName + " Data Change");

				lampListData.Update();
			});
		}

		#region Data management
		protected override List<LampListData> CollectData()
		{
			List<LampListData> data = new List<LampListData>();

			foreach (var mappingsLampData in TableComponent.MappingConfig.Lamps) {
				data.Add(new LampListData(mappingsLampData));
			}

			RefreshLampIds();

			return data;
		}

		protected override void AddNewData(string undoName, string newName)
		{
			RecordUndo(undoName);
			TableComponent.MappingConfig.AddLamp(new LampMapping());
		}

		protected override void RemoveData(string undoName, LampListData data)
		{
			RecordUndo(undoName);
			TableComponent.MappingConfig.RemoveLamp(data.LampMapping);
		}

		protected override void CloneData(string undoName, string newName, LampListData data)
		{
			RecordUndo(undoName);

			TableComponent.MappingConfig.AddLamp(new LampMapping {
				Id = data.Id,
				InternalId = data.InternalId,
				Description = data.Description,
				Device = data.Device,
				DeviceItem = data.DeviceItem,
				Type = data.Type,
				Source = data.Source,
				IsCoil = data.IsCoil,
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

		private void RefreshLampIds()
		{
			_gleLamps.Clear();
			_gleLamps.AddRange(TableComponent.MappingConfig.GetLamps(GetAvailableEngineLamps()));
		}

		private GamelogicEngineLamp[] GetAvailableEngineLamps()
		{
			var gle = TableComponent.gameObject.GetComponent<IGamelogicEngine>();
			return gle == null ? Array.Empty<GamelogicEngineLamp>() : gle.AvailableLamps;
		}

		#endregion

		#region Undo Redo

		private void RecordUndo(string undoName)
		{
			Undo.RecordObjects(new Object[] { this, TableComponent }, undoName);
		}

		#endregion
	}

	internal enum ToggleAction
	{
		All, Inserts, GI, Flasher
	}
}
