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

using VisualPinball.Engine.VPT.Mappings;

namespace VisualPinball.Unity.Editor
{
	public class LampListData : IManagerListData
	{
		[ManagerListColumn(Order = 0, HeaderName = "ID", Width = 135)]
		public string Name => Id;

		[ManagerListColumn(Order = 1, HeaderName = "Description", Width = 150)]
		public string Description;

		[ManagerListColumn(Order = 2, HeaderName = "Destination", Width = 150)]
		public int Destination;

		[ManagerListColumn(Order = 3, HeaderName = "Element", Width = 200)]
		public string Element;

		[ManagerListColumn(Order = 4, HeaderName = "Type", Width = 110)]
		public int Type;

		[ManagerListColumn(Order = 5, HeaderName = "R G B", Width = 300)]
		public string Green;
		public string Blue;

		public string Id;
		public string PlayfieldItem;
		public string Device;
		public string DeviceItem;
		public int Source;

		public MappingsLampData MappingsLampData;

		public LampListData(MappingsLampData mappingsLampData)
		{
			Id = mappingsLampData.Id;
			Source = mappingsLampData.Source;
			Description = mappingsLampData.Description;
			PlayfieldItem = mappingsLampData.PlayfieldItem;
			Device = mappingsLampData.Device;
			DeviceItem = mappingsLampData.DeviceItem;
			Type = mappingsLampData.Type;
			Green = mappingsLampData.Green;
			Blue = mappingsLampData.Blue;

			MappingsLampData = mappingsLampData;
		}

		public void Update()
		{
			MappingsLampData.Id = Id;
			MappingsLampData.Source = Source;
			MappingsLampData.Description = Description;
			MappingsLampData.PlayfieldItem = PlayfieldItem;
			MappingsLampData.Device = Device;
			MappingsLampData.DeviceItem = DeviceItem;
			MappingsLampData.Type = Type;
			MappingsLampData.Green = Green;
			MappingsLampData.Blue = Blue;
		}
	}
}
