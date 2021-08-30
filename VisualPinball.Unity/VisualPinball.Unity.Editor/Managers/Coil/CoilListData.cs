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

namespace VisualPinball.Unity.Editor
{
	public class CoilListData : IManagerListData
	{
		[ManagerListColumn(Order = 0, HeaderName = "ID", Width = 135)]
		public string Name => Id;

		[ManagerListColumn(Order = 1, HeaderName = "Description", Width = 150)]
		public string Description;

		[ManagerListColumn(Order = 2, HeaderName = "Destination", Width = 150)]
		public ECoilDestination Destination;

		[ManagerListColumn(Order = 3, HeaderName = "Element", Width = 270)]
		public string Element;

		[ManagerListColumn(Order = 4, HeaderName = "Type", Width = 110)]
		public ECoilType Type;

		[ManagerListColumn(Order = 5, HeaderName = "Hold Coil", Width = 135)]
		public string HoldCoilId;

		public string Id;
		public int InternalId;
		public ICoilDeviceAuthoring Device;
		public string DeviceCoilId;

		public readonly CoilMapping CoilMapping;

		public CoilListData(CoilMapping coilMapping) {
			Id = coilMapping.Id;
			InternalId = coilMapping.InternalId;
			Description = coilMapping.Description;
			Destination = coilMapping.Destination;
			Device = coilMapping.Device;
			DeviceCoilId = coilMapping.DeviceCoilId;
			Type = coilMapping.Type;
			HoldCoilId = coilMapping.HoldCoilId;

			CoilMapping = coilMapping;
		}

		public void Update()
		{
			CoilMapping.Id = Id;
			CoilMapping.InternalId = InternalId;
			CoilMapping.Description = Description;
			CoilMapping.Destination = Destination;
			CoilMapping.Device = Device;
			CoilMapping.DeviceCoilId = DeviceCoilId;
			CoilMapping.Type = Type;
			CoilMapping.HoldCoilId = HoldCoilId;
		}
	}
}
