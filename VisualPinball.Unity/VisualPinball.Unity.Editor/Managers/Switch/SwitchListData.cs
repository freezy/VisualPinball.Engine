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
	public class SwitchListData : IManagerListData
	{
		[ManagerListColumn(Order = 0, HeaderName = "ID", Width = 120)]
		public string Name => Id;

		[ManagerListColumn(Order = 1, HeaderName = "Description", Width = 150)]
		public string Description;

		[ManagerListColumn(Order = 2, HeaderName = "Source", Width = 150)]
		public int Source;

		[ManagerListColumn(Order = 3, HeaderName = "Element", Width = 200)]
		public string Element;

		[ManagerListColumn(Order = 4, HeaderName = "Type", Width = 100)]
		public int Type;

		[ManagerListColumn(Order = 5, HeaderName = "Off", Width = 100)]
		public string Off;

		public string Id;
		public string InputActionMap;
		public string InputAction;
		public string PlayfieldItem;
		public int Constant;
		public string Device;
		public string DeviceItem;
		public int Pulse;

		public MappingsSwitchData MappingsSwitchData;

		public SwitchListData(MappingsSwitchData mappingsSwitchData) {
			Id = mappingsSwitchData.Id;
			Description = mappingsSwitchData.Description;
			Source = mappingsSwitchData.Source;
			InputActionMap = mappingsSwitchData.InputActionMap;
			InputAction = mappingsSwitchData.InputAction;
			PlayfieldItem = mappingsSwitchData.PlayfieldItem;
			Constant = mappingsSwitchData.Constant;
			Device = mappingsSwitchData.Device;
			DeviceItem = mappingsSwitchData.DeviceItem;
			Type = mappingsSwitchData.Type;
			Pulse = mappingsSwitchData.Pulse;

			MappingsSwitchData = mappingsSwitchData;
		}

		public void Update()
		{
			MappingsSwitchData.Id = Id;
			MappingsSwitchData.Description = Description;
			MappingsSwitchData.Source = Source;
			MappingsSwitchData.InputActionMap = InputActionMap;
			MappingsSwitchData.InputAction = InputAction;
			MappingsSwitchData.PlayfieldItem = PlayfieldItem;
			MappingsSwitchData.Constant = Constant;
			MappingsSwitchData.Device = Device;
			MappingsSwitchData.DeviceItem = DeviceItem;
			MappingsSwitchData.Type = Type;
			MappingsSwitchData.Pulse = Pulse;
		}
	}
}
