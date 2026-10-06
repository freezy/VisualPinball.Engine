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

		/// <summary>The lamp settings row, on its own line above the toolbar, when it is open.</summary>
		protected override void OnGUI()
		{
			DrawLampSettings();
			base.OnGUI();
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

			DrawLampPreset();
			DrawInsertBrightness();

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

		/// <summary>
		/// The kind of bulb the table is lit by: one button per lamp preset,
		/// the inserts' own lit, that puts the chosen bulb under every insert
		/// and its fades on every other lamp, flashers aside. Buttons, not a
		/// dropdown: a dropdown opens a native menu that holds the editor, and
		/// the game with it, until it closes, and this is meant to be used
		/// during a game in the editor, so a table can be judged under both.
		/// </summary>
		private void DrawLampPreset()
		{
			if (TableComponent == null) {
				return;
			}
			var presets = InsertLampPresets.Find();
			var current = InsertLampPresets.TableLamp(TableComponent, out var mixed);
			GUILayout.Space(8f);
			GUILayout.Label(new GUIContent("Lamp Preset:", "The kind of bulb the table is lit by: its colour, output and fades go under every insert, and its fades to every other lamp but the flashers. The lit button is the inserts'; none lit, they follow none or not all the same."), GUILayout.ExpandWidth(false));
			foreach (var preset in presets) {
				var on = !mixed && preset == current;
				if (GUILayout.Toggle(on, new GUIContent(preset.LampName, $"{preset.LampName}: colour {preset.Color}, output {preset.Output:0.##}, fades {preset.FadeSpeedUp:0.##} s up and {preset.FadeSpeedDown:0.##} s down."), EditorStyles.miniButton, GUILayout.ExpandWidth(false)) && !on) {
					var (inserts, lamps) = InsertLampPresets.ApplyToTable(preset, TableComponent);
					Logger.Info($"Lamp Manager: {preset.LampName} under {inserts} inserts and {lamps} other lamps.");
				}
			}
			if (current != null && !mixed) {
				_lampSettings = GUILayout.Toggle(_lampSettings, new GUIContent("Settings", $"Edit the {current.LampName} lamp here: its colour, output and fades go to the preset, and from there under every insert of the table, as you change them."), EditorStyles.miniButton, GUILayout.ExpandWidth(false));
			}
			InsertPresetGui.EditButton(InsertPresetWindow.Kind.Lamp, current);
		}

		private static bool _lampSettings;

		/// <summary>
		/// The table's insert brightness: a factor on every insert's output and
		/// spill, and the default the player starts the table with until its
		/// user sets another. A field, not a slider: it's the author's number,
		/// and works while the game runs.
		/// </summary>
		private void DrawInsertBrightness()
		{
			if (TableComponent == null) {
				return;
			}
			GUILayout.Space(8f);
			GUILayout.Label(new GUIContent("Insert Brightness:", "A factor on every insert's output and spill. It's the table's default: the player lets its user change it per table."), GUILayout.ExpandWidth(false));
			EditorGUI.BeginChangeCheck();
			var brightness = EditorGUILayout.FloatField(TableComponent.InsertBrightness, GUILayout.Width(40f));
			if (EditorGUI.EndChangeCheck()) {
				RecordUndo("Change Insert Brightness");
				TableComponent.InsertBrightness = Mathf.Max(0f, brightness);
				EditorUtility.SetDirty(TableComponent);
				PrefabUtility.RecordPrefabInstancePropertyModifications(TableComponent);
				foreach (var insert in TableComponent.GetComponentsInChildren<InsertLightComponent>(true)) {
					insert.Apply();
				}
				SceneView.RepaintAll();
			}
		}

		/// <summary>
		/// The table's lamp, editable where it is used: the preset's four values,
		/// written to the preset and put under every insert as they change. No
		/// black box, and nothing that pauses a running game.
		/// </summary>
		private void DrawLampSettings()
		{
			if (!_lampSettings || TableComponent == null) {
				return;
			}
			var current = InsertLampPresets.TableLamp(TableComponent, out var mixed);
			if (current == null || mixed) {
				return;
			}
			using (new EditorGUILayout.HorizontalScope()) {
				GUILayout.Space(8f);
				GUILayout.Label(new GUIContent(current.LampName, "The lamp preset asset these values belong to."), EditorStyles.boldLabel, GUILayout.ExpandWidth(false));
				EditorGUI.BeginChangeCheck();
				EditorGUIUtility.labelWidth = 44f;
				var color = EditorGUILayout.ColorField(new GUIContent("Color", "The light the bulb gives; it multiplies the plastic's Lamp Emission."), current.Color, GUILayout.Width(110f));
				EditorGUIUtility.labelWidth = 48f;
				var output = EditorGUILayout.FloatField(new GUIContent("Output", "How much light the bulb gives, relative to a #44 filament."), current.Output, GUILayout.Width(90f));
				EditorGUIUtility.labelWidth = 56f;
				var up = EditorGUILayout.FloatField(new GUIContent("Fade up", "Seconds to full intensity."), current.FadeSpeedUp, GUILayout.Width(100f));
				EditorGUIUtility.labelWidth = 66f;
				var down = EditorGUILayout.FloatField(new GUIContent("Fade down", "Seconds to off."), current.FadeSpeedDown, GUILayout.Width(110f));
				EditorGUIUtility.labelWidth = 0f;
				if (EditorGUI.EndChangeCheck()) {
					Undo.RecordObject(current, "Edit Insert Lamp Preset");
					current.Color = color;
					current.Output = Mathf.Max(0f, output);
					current.FadeSpeedUp = Mathf.Max(0f, up);
					current.FadeSpeedDown = Mathf.Max(0f, down);
					EditorUtility.SetDirty(current);
					InsertLampPresets.ApplyToTable(current, TableComponent);
				}
				if (GUILayout.Button(new GUIContent("Asset", "Highlights the preset asset in the Project window."), EditorStyles.miniButton, GUILayout.ExpandWidth(false))) {
					EditorGUIUtility.PingObject(current);
				}
			}
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
			if (!TableComponent) {
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
				if (Application.isPlaying && player) {
					try {
						// The lamp the mapping names on the device, one bulb of an insert
						// with several, or the device's own.
						var api = player.Lamp(device, lampMapping.DeviceItem) ?? device.GetApi(player);
						api.OnLamp(enabled ? LampStatus.On : LampStatus.Off);
						handledByApi = true;
					} catch (Exception ex) {
						Logger.Warn(ex, $"Failed to toggle lamp via API for device \"{(device as Component)?.name}\".");
					}
				}

				if (!handledByApi) {
					SetLampDeviceEnabled(device, lampMapping.DeviceItem, enabled);
				}
			}
		}

		private static void SetLampDeviceEnabled(ILampDeviceComponent device, string deviceItem, bool enabled)
		{
			// In edit mode LightComponent runtime caches are not initialized yet,
			// so directly toggling Unity lights keeps the manager buttons responsive.
			foreach (var light in device.LightSources.Where(light => light)) {
				light.enabled = enabled;
			}

			switch (device) {
				case InsertLightComponent insert:
					insert.SetBulbEnabled(deviceItem, enabled);
					break;
				case LightComponent lightComponent:
					lightComponent.Enabled = enabled;
					break;
				case LightGroupComponent lightGroup:
					// each light with the lamp the group drives of it, like a single bulb of an insert.
					for (var i = 0; i < lightGroup._lights.Length; i++) {
						if (lightGroup._lights[i] is ILampDeviceComponent child) {
							SetLampDeviceEnabled(child, lightGroup.LampId(i) ?? deviceItem, enabled);
						}
					}
					break;
				default:
					foreach (var light in device.LightSources.Where(light => light)) {
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