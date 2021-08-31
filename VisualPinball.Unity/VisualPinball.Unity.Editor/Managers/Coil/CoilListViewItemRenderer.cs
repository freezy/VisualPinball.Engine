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
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Mappings;
using Texture = UnityEngine.Texture;

namespace VisualPinball.Unity.Editor
{
	public class CoilListViewItemRenderer
	{
		private enum CoilListColumn
		{
			Id = 0,
			Description = 1,
			Destination = 2,
			Element = 3,
			Type = 4,
			HoldCoilId = 5,
		}

		private readonly TableAuthoring _tableComponent;
		private readonly List<GamelogicEngineCoil> _gleCoils;

		private readonly ObjectReferencePicker<ICoilDeviceAuthoring> _devicePicker;

		public CoilListViewItemRenderer(TableAuthoring tableComponent, List<GamelogicEngineCoil> gleCoils)
		{
			_tableComponent = tableComponent;
			_gleCoils = gleCoils;
			_devicePicker = new ObjectReferencePicker<ICoilDeviceAuthoring>("Coil Devices", tableComponent, false);
		}

		public void Render(TableAuthoring tableAuthoring, CoilListData data, Rect cellRect, int column, Action<CoilListData> updateAction)
		{
			EditorGUI.BeginDisabledGroup(Application.isPlaying);
			var coilStatuses = Application.isPlaying
				? tableAuthoring.gameObject.GetComponent<Player>()?.CoilStatuses
				: null;

			switch ((CoilListColumn)column)
			{
				case CoilListColumn.Id:
					RenderId(coilStatuses, ref data.Id, id => UpdateId(data, id), data, cellRect, updateAction);
					break;
				case CoilListColumn.Description:
					RenderDescription(data, cellRect, updateAction);
					break;
				case CoilListColumn.Destination:
					RenderDestination(data, cellRect, updateAction);
					break;
				case CoilListColumn.Element:
					RenderElement(data, cellRect, updateAction);
					break;
				case CoilListColumn.Type:
					RenderType(data, cellRect, updateAction);
					break;
				case CoilListColumn.HoldCoilId:
					if (data.Type == ECoilType.DualWound) {
						RenderId(coilStatuses, ref data.HoldCoilId, id => data.HoldCoilId = id, data, cellRect, updateAction);
					}
					break;
			}
			EditorGUI.EndDisabledGroup();
		}

		private void UpdateId(CoilListData data, string id)
		{
			if (data.Destination == ECoilDestination.Lamp) {
				var lampEntry = _tableComponent.Mappings.Lamps.FirstOrDefault(l => l.Id == data.Id && l.Source == LampSource.Coils);
				if (lampEntry != null) {
					lampEntry.Id = id;
					LampManager.Refresh();
				}
			}
			data.Id = id;
		}

		private void RenderId(Dictionary<string, bool> coilStatuses, ref string id, Action<string> setId, CoilListData coilListData, Rect cellRect, Action<CoilListData> updateAction)
		{
			const float idWidth = 25f;
			const float padding = 2f;

			// add some padding
			cellRect.x += padding;
			cellRect.width -= 2 * padding;

			var dropdownRect = cellRect;
			dropdownRect.width -= idWidth + 2 * padding;

			var idRect = cellRect;
			idRect.width = idWidth;
			idRect.x += cellRect.width - idWidth;

			var options = new List<string>(_gleCoils.Select(entry => entry.Id).ToArray());
			if (options.Count > 0) {
				options.Add("");
			}
			options.Add("Add...");

			if (Application.isPlaying && coilStatuses != null) {
				var iconRect = cellRect;
				iconRect.width = 20;
				dropdownRect.x += 25;
				dropdownRect.width -= 25;
				if (coilStatuses.ContainsKey(id)) {
					var coilStatus = coilStatuses[id];
					var icon = Icons.Bolt(IconSize.Small, coilStatus ? IconColor.Orange : IconColor.Gray);
					var guiColor = GUI.color;
					GUI.color = Color.clear;
					EditorGUI.DrawTextureTransparent(iconRect, icon, ScaleMode.ScaleToFit);
					GUI.color = guiColor;
				}
			}

			EditorGUI.BeginChangeCheck();
			var index = EditorGUI.Popup(dropdownRect, options.IndexOf(id), options.ToArray());
			if (EditorGUI.EndChangeCheck()) {
				if (index == options.Count - 1) {
					PopupWindow.Show(dropdownRect, new ManagerListTextFieldPopup("ID", "", newId => {
						if (!_gleCoils.Exists(entry => entry.Id == newId)) {
							_gleCoils.Add(new GamelogicEngineCoil(newId));
						}

						setId(newId);
						updateAction(coilListData);
					}));

				} else {
					setId(_gleCoils[index].Id);
					updateAction(coilListData);
				}
			}

			EditorGUI.BeginChangeCheck();
			var value = EditorGUI.IntField(idRect, coilListData.InternalId);
			if (EditorGUI.EndChangeCheck()) {
				coilListData.InternalId = value;
				updateAction(coilListData);
			}
		}

		private void RenderDescription(CoilListData coilListData, Rect cellRect, Action<CoilListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var value = EditorGUI.TextField(cellRect, coilListData.Description);
			if (EditorGUI.EndChangeCheck())
			{
				coilListData.Description = value;
				updateAction(coilListData);
			}
		}

		private void RenderDestination(CoilListData coilListData, Rect cellRect, Action<CoilListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var index = (ECoilDestination)EditorGUI.EnumPopup(cellRect, coilListData.Destination);
			if (EditorGUI.EndChangeCheck())
			{
				if (coilListData.Destination != index)
				{
					if (coilListData.Destination == ECoilDestination.Lamp) {

						var lampEntry = _tableComponent.Mappings.Lamps.FirstOrDefault(l => l.Id == coilListData.Id && l.Source == LampSource.Coils);
						if (lampEntry != null) {
							_tableComponent.Mappings.RemoveLamp(lampEntry);
							LampManager.Refresh();
						}

					} else if (index == ECoilDestination.Lamp) {
						_tableComponent.Mappings.AddLamp(new MappingsLampData {
							Id = coilListData.Id,
							Source = LampSource.Coils,
							Description = coilListData.Description
						});
						LampManager.Refresh();
					}
					coilListData.Destination = index;
					updateAction(coilListData);
				}
			}
		}

		private void RenderElement(CoilListData coilListData, Rect cellRect, Action<CoilListData> updateAction)
		{
			var icon = GetIcon(coilListData);

			if (icon != null)
			{
				var iconRect = cellRect;
				iconRect.width = 20;
				var guiColor = GUI.color;
				GUI.color = Color.clear;
				EditorGUI.DrawTextureTransparent(iconRect, icon, ScaleMode.ScaleToFit);
				GUI.color = guiColor;
			}

			cellRect.x += 25;
			cellRect.width -= 25;

			switch (coilListData.Destination)
			{
				case ECoilDestination.Playfield:
					cellRect.width = cellRect.width / 2f - 5f;
					RenderDeviceElement(coilListData, cellRect, updateAction);
					cellRect.x += cellRect.width + 10f;
					RenderDeviceItemElement(coilListData, cellRect, updateAction);
					break;

				case ECoilDestination.Lamp:
					cellRect.x -= 25;
					cellRect.width += 25;
					EditorGUI.LabelField(cellRect, "Configure in Lamp Manager", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Italic });
					break;
			}
		}

		private void RenderDeviceElement(CoilListData coilListData, Rect cellRect, Action<CoilListData> updateAction)
		{
			_devicePicker.Render(cellRect, coilListData.Device, item => {
				coilListData.Device = item;
				if (coilListData.Device != null && coilListData.Device.AvailableCoils.Count() == 1) {
					coilListData.DeviceCoilId = coilListData.Device.AvailableCoils.First().Id;
				}
				updateAction(coilListData);
			});
		}

		private void RenderDeviceItemElement(CoilListData coilListData, Rect cellRect, Action<CoilListData> updateAction)
		{
			EditorGUI.BeginDisabledGroup(coilListData.Device == null);

			var currentIndex = 0;
			var coilLabels = Array.Empty<string>();
			ICoilDeviceAuthoring coilDevice = null;
			if (coilListData.Device != null) {
				coilDevice = coilListData.Device;
				coilLabels = coilDevice.AvailableCoils.Select(s => s.Description).ToArray();
				currentIndex = coilDevice.AvailableCoils.TakeWhile(s => s.Id != coilListData.DeviceCoilId).Count();
			}
			EditorGUI.BeginChangeCheck();
			var newIndex = EditorGUI.Popup(cellRect, currentIndex, coilLabels);
			if (EditorGUI.EndChangeCheck() && coilDevice != null) {
				if (currentIndex != newIndex) {
					coilListData.DeviceCoilId = coilDevice.AvailableCoils.ElementAt(newIndex).Id;
					updateAction(coilListData);
				}
			}
			EditorGUI.EndDisabledGroup();
		}

		private void RenderType(CoilListData coilListData, Rect cellRect, Action<CoilListData> updateAction)
		{
			if (coilListData.Destination == CoilDestination.Playfield)
			{
				EditorGUI.BeginChangeCheck();
				var type = (ECoilType)EditorGUI.EnumPopup(cellRect, coilListData.Type);
				if (EditorGUI.EndChangeCheck()) {
					coilListData.Type = type;
					updateAction(coilListData);
				}
			}
		}

		private Texture GetIcon(CoilListData coilListData)
		{
			Texture2D icon = null;

			switch (coilListData.Destination)
			{
				case ECoilDestination.Playfield:
					if (coilListData.Device != null) {
						icon = Icons.ByComponent(coilListData.Device, IconSize.Small);
					}
					break;
			}

			return icon;
		}
	}
}
