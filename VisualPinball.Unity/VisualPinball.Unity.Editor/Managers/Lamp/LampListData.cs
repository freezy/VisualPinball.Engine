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

using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity.Editor
{
	public class LampListData : IManagerListData, IDeviceListData<GamelogicEngineLamp>
	{
		[ManagerListColumn(Order = 0, HeaderName = "ID", Width = 135)]
		public string Name => Id;

		public int InternalId { get; set; }

		[ManagerListColumn(Order = 1, HeaderName = "Description", Width = 150)]
		public string Description { get; set; }

		[ManagerListColumn(Order = 2, HeaderName = "Element", Width = 200)]
		public string Element;

		[ManagerListColumn(Order = 3, HeaderName = "Type", Width = 110)]
		public LampType Type;

		[ManagerListColumn(Order = 4, HeaderName = "R G B", Width = 300)]
		public string Green;
		public string Blue;

		public string Id;
		public string PlayfieldItem;
		public ILampDeviceComponent Device;
		public string DeviceItem { get; set; }
		public LampSource Source;

		public LampMapping LampMapping;
		
		public IDeviceComponent<GamelogicEngineLamp> DeviceComponent => Device;

		public LampListData(LampMapping lampMapping)
		{
			Id = lampMapping.Id;
			Source = lampMapping.Source;
			Description = lampMapping.Description;
			Device = lampMapping.Device;
			DeviceItem = lampMapping.DeviceItem;
			Type = lampMapping.Type;
			Green = lampMapping.Green;
			Blue = lampMapping.Blue;

			LampMapping = lampMapping;
		}

		public void Update()
		{
			LampMapping.Id = Id;
			LampMapping.Source = Source;
			LampMapping.Description = Description;
			LampMapping.Device = Device;
			LampMapping.DeviceItem = DeviceItem;
			LampMapping.Type = Type;
			LampMapping.Green = Green;
			LampMapping.Blue = Blue;
		}
	}
}
