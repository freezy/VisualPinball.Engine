// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using VisualPinball.Engine.VPT.Mappings;

namespace VisualPinball.Unity.Editor
{
	public class CoilListData : IManagerListData
	{
		[ManagerListColumn(Order = 0, HeaderName = "ID", Width = 135)]
		public string Name => Id;

		[ManagerListColumn(Order = 1, HeaderName = "Description", Width = 150)]
		public string Description;

		[ManagerListColumn(Order = 2, HeaderName = "Destination", Width = 150)]
		public int Destination;

		[ManagerListColumn(Order = 3, HeaderName = "Element", Width = 270)]
		public string Element;

		[ManagerListColumn(Order = 4, HeaderName = "Type", Width = 110)]
		public int Type;

		[ManagerListColumn(Order = 6, HeaderName = "Hold Coil", Width = 135)]
		public string HoldCoilId;

		public string Id;
		public string PlayfieldItem;
		public string Device;
		public string DeviceItem;

		public MappingsCoilData MappingsCoilData;

		public CoilListData(MappingsCoilData mappingsCoilData) {
			Id = mappingsCoilData.Id;
			Description = mappingsCoilData.Description;
			Destination = mappingsCoilData.Destination;
			PlayfieldItem = mappingsCoilData.PlayfieldItem;
			Device = mappingsCoilData.Device;
			DeviceItem = mappingsCoilData.DeviceItem;
			Type = mappingsCoilData.Type;
			HoldCoilId = mappingsCoilData.HoldCoilId;

			MappingsCoilData = mappingsCoilData;
		}

		public void Update()
		{
			MappingsCoilData.Id = Id;
			MappingsCoilData.Description = Description;
			MappingsCoilData.Destination = Destination;
			MappingsCoilData.PlayfieldItem = PlayfieldItem;
			MappingsCoilData.Device = Device;
			MappingsCoilData.DeviceItem = DeviceItem;
			MappingsCoilData.Type = Type;
			MappingsCoilData.HoldCoilId = HoldCoilId;
		}
	}
}
