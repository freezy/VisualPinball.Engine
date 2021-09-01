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

namespace VisualPinball.Unity.Editor
{
	public class CoilListViewItemRenderer : ListViewItemRenderer<CoilListData, GamelogicEngineCoil>
	{
		protected override List<GamelogicEngineCoil> GleItems { get; }
		protected override GamelogicEngineCoil InstantiateGleItem(string id) => new GamelogicEngineCoil(id);
		protected override Texture2D StatusIcon(bool status) => Icons.Bolt(IconSize.Small, status ? IconColor.Orange : IconColor.Gray);

		private enum CoilListColumn
		{
			Id = 0,
			Description = 1,
			Destination = 2,
			Element = 3
		}

		private readonly TableAuthoring _tableComponent;

		private readonly ObjectReferencePicker<ICoilDeviceAuthoring> _devicePicker;

		public CoilListViewItemRenderer(TableAuthoring tableComponent, List<GamelogicEngineCoil> gleCoils)
		{
			_tableComponent = tableComponent;
			GleItems = gleCoils;
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
			}
			EditorGUI.EndDisabledGroup();
		}

		private void UpdateId(CoilListData data, string id)
		{
			if (data.Destination == CoilDestination.Lamp) {
				var lampEntry = _tableComponent.MappingConfig.Lamps.FirstOrDefault(l => l.Id == data.Id && l.Source == LampSource.Coils);
				if (lampEntry != null) {
					lampEntry.Id = id;
					LampManager.Refresh();
				}
			}
			data.Id = id;
		}

		private void RenderDestination(CoilListData coilListData, Rect cellRect, Action<CoilListData> updateAction)
		{
			EditorGUI.BeginChangeCheck();
			var index = (CoilDestination)EditorGUI.EnumPopup(cellRect, coilListData.Destination);
			if (EditorGUI.EndChangeCheck())
			{
				if (coilListData.Destination != index)
				{
					if (coilListData.Destination == CoilDestination.Lamp) {

						var lampEntry = _tableComponent.MappingConfig.Lamps.FirstOrDefault(l => l.Id == coilListData.Id && l.Source == LampSource.Coils);
						if (lampEntry != null) {
							_tableComponent.MappingConfig.RemoveLamp(lampEntry);
							LampManager.Refresh();
						}

					} else if (index == CoilDestination.Lamp) {
						_tableComponent.MappingConfig.AddLamp(new LampMapping {
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
			switch (coilListData.Destination) {
				case CoilDestination.Playfield:
					RenderDevice(coilListData, cellRect, updateAction);
					break;

				case CoilDestination.Lamp:
					cellRect = RenderIcon(coilListData, cellRect);
					cellRect.x -= 25;
					cellRect.width += 25;
					EditorGUI.LabelField(cellRect, "Configure in Lamp Manager", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Italic });
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		protected override void RenderDeviceElement(CoilListData coilListData, Rect cellRect, Action<CoilListData> updateAction)
		{
			_devicePicker.Render(cellRect, coilListData.Device, item => {
				coilListData.Device = item;
				UpdateDeviceItem(coilListData);
				updateAction(coilListData);
			});
		}

		protected override Texture GetIcon(CoilListData coilListData)
		{
			Texture2D icon = null;

			switch (coilListData.Destination) {
				case CoilDestination.Playfield:
					if (coilListData.Device != null) {
						icon = Icons.ByComponent(coilListData.Device, IconSize.Small);
					}
					break;

				case CoilDestination.Lamp:
					icon = Icons.Light(IconSize.Small);
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}

			return icon;
		}
	}
}
