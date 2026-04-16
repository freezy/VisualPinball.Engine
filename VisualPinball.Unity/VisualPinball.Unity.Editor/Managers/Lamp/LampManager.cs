// Visual Pinball Engine
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
		private bool _toggleSource = true;

		public static void Refresh()
		{
			if (HasOpenInstances<LampManager>()) {
				var f = focusedWindow;
				GetWindow<LampManager>().Reload();
				FocusWindowIfItsOpen(f.GetType());
			}
		}

		[MenuItem("Pinball/Lamp Manager", false, 304)]
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
				ToggleLampState(true);
			}
			if (GUILayout.Button("Turn Off", GUILayout.ExpandWidth(false))) {
				ToggleLampState(false);
			}
			if (GUILayout.Button("Select", GUILayout.ExpandWidth(false))) {
				var lights = new List<Light>();
				Toggle(lights.Add);
				Selection.objects = _toggleSource
					? lights.Select(l => l.gameObject as Object).ToArray()
					: lights.Select(l => l.gameObject.transform.parent.gameObject as Object).ToArray();
			}
			_toggleSource = GUILayout.Toggle(_toggleSource, "Source");
		}

		private void Toggle(Action<Light> action)
		{
			if (TableComponent != null) {
				foreach (var lampMapping in GetSelectedMappings()) {
					if (lampMapping.Device == null) {
						continue;
					}

					foreach (var light in lampMapping.Device.LightSources) {
						action(light);
					}
				}
			}
		}

		private IEnumerable<LampMapping> GetSelectedMappings()
		{
			return _toggleAction switch {
				ToggleAction.All => TableComponent.MappingConfig.Lamps,
				ToggleAction.Inserts => TableComponent.MappingConfig.Lamps.Where(lm => !lm.IsCoil && lm.Source == LampSource.Lamp),
				ToggleAction.GI => TableComponent.MappingConfig.Lamps.Where(lm => lm.Source == LampSource.GI),
				ToggleAction.Flasher => TableComponent.MappingConfig.Lamps.Where(lm => lm.IsCoil),
				ToggleAction.Selected => _listView.GetSelectedData().Select(lld => lld.LampMapping),
				_ => throw new ArgumentOutOfRangeException()
			};
		}

		private void ToggleLampState(bool enabled)
		{
			if (TableComponent == null) {
				return;
			}

			// In play mode, drive lamp APIs so LightComponent updates both Unity lights and emissive materials.
			// Outside play mode, fall back to directly toggling the runtime light components.
			var player = TableComponent.GetComponentInParent<Player>() ?? TableComponent.GetComponentInChildren<Player>();
			foreach (var lampMapping in GetSelectedMappings()) {
				var device = lampMapping.Device;
				if (device == null) {
					continue;
				}

				var handledByApi = false;
				if (Application.isPlaying && player != null) {
					try {
						device.GetApi(player).OnLamp(enabled ? LampStatus.On : LampStatus.Off);
						handledByApi = true;
					} catch (Exception ex) {
						Logger.Warn(ex, $"Failed to toggle lamp via API for device \"{(device as Component)?.name}\".");
					}
				}

				if (!handledByApi) {
					SetLampDeviceEnabled(device, enabled, Application.isPlaying);
				}
			}
		}

		private static void SetLampDeviceEnabled(ILampDeviceComponent device, bool enabled, bool isPlaying)
		{
			// In edit mode LightComponent runtime caches are not initialized yet,
			// so directly toggling Unity lights keeps the manager buttons responsive.
			if (!isPlaying) {
				foreach (var light in device.LightSources.Where(light => light != null)) {
					light.enabled = enabled;
				}
				return;
			}

			switch (device) {
				case LightComponent lightComponent:
					lightComponent.Enabled = enabled;
					break;
				case LightGroupComponent lightGroup:
					foreach (var child in lightGroup.Lights.Where(child => child != null)) {
						SetLampDeviceEnabled(child, enabled, isPlaying);
					}
					break;
				default:
					foreach (var light in device.LightSources.Where(light => light != null)) {
						light.enabled = enabled;
					}
					break;
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
			return gle == null ? Array.Empty<GamelogicEngineLamp>() : gle.RequestedLamps;
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
		All, Inserts, GI, Flasher, Selected
	}
}
