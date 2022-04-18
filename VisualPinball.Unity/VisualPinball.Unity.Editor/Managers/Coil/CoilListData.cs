// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

using System.Linq;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity.Editor
{
	public class CoilListData : IManagerListData, IDeviceListData<GamelogicEngineCoil>
	{
		[ManagerListColumn(Order = 0, HeaderName = "ID", Width = 135)]
		public string Name => Id;

		[ManagerListColumn(Order = 1, HeaderName = "Description", Width = 150)]
		public string Description { get; set; }

		[ManagerListColumn(Order = 2, HeaderName = "Destination", Width = 150)]
		public CoilDestination Destination;

		[ManagerListColumn(Order = 3, HeaderName = "Element", Width = 270)]
		public string Element;

		public string Id;
		public ICoilDeviceComponent Device;
		public string DeviceItem { get; set; }

		public readonly CoilMapping CoilMapping;

		public IDeviceComponent<GamelogicEngineCoil> DeviceComponent => Device;

		public CoilListData(CoilMapping coilMapping) {
			Id = coilMapping.Id;
			Description = coilMapping.Description;
			Destination = coilMapping.Destination;
			Device = coilMapping.Device;
			if (string.IsNullOrEmpty(coilMapping.DeviceItem) && Device != null && Device.AvailableCoils.Count() == 1) {
				DeviceItem = Device.AvailableCoils.First().Id;

			} else {
				DeviceItem = coilMapping.DeviceItem;
			}

			CoilMapping = coilMapping;
		}

		public void Update()
		{
			CoilMapping.Id = Id;
			CoilMapping.Description = Description;
			CoilMapping.Destination = Destination;
			CoilMapping.Device = Device;
			CoilMapping.DeviceItem = DeviceItem;
		}

		public void ClearDevice()
		{
			Device = null;
			DeviceItem = null;
			Update();
		}
	}
}
