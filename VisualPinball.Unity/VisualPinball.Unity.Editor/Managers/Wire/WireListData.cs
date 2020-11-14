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
	public class WireListData : IManagerListData
	{
		[ManagerListColumn(Order = 0, HeaderName = "Description", Width = 150)]
		public string Name => Description;

		[ManagerListColumn(Order = 1, HeaderName = "Source", Width = 150)]
		public int Source;

		[ManagerListColumn(Order = 2, HeaderName = "Source Element", Width = 270)]
		public string SourceElement;

		[ManagerListColumn(Order = 3, HeaderName = "Destination", Width = 150)]
		public int Destination;

		[ManagerListColumn(Order = 4, HeaderName = "Destination Element", Width = 270)]
		public string DestinationElement;

		[ManagerListColumn(Order = 5, HeaderName = "Pulse Delay", Width = 100)]
		public int PulseDelay;

		public string Description;

		public string SourceInputActionMap;
		public string SourceInputAction;
		public string SourcePlayfieldItem;
		public int SourceConstant;
		public string SourceDevice;
		public string SourceDeviceItem;

		public string DestinationPlayfieldItem;
		public string DestinationDevice;
		public string DestinationDeviceItem;

		public MappingsWireData MappingsWireData;

		public WireListData(MappingsWireData mappingsWireData) {
			Description = mappingsWireData.Description;

			Source = mappingsWireData.Source;
			SourceInputActionMap = mappingsWireData.SourceInputActionMap;
			SourceInputAction = mappingsWireData.SourceInputAction;
			SourcePlayfieldItem = mappingsWireData.SourcePlayfieldItem;
			SourceConstant = mappingsWireData.SourceConstant;
			SourceDevice = mappingsWireData.SourceDevice;
			SourceDeviceItem = mappingsWireData.SourceDeviceItem;

			Destination = mappingsWireData.Destination;
			DestinationPlayfieldItem = mappingsWireData.DestinationPlayfieldItem;
			DestinationDevice = mappingsWireData.DestinationDevice;
			DestinationDeviceItem = mappingsWireData.DestinationDeviceItem;

			PulseDelay = mappingsWireData.PulseDelay;

			MappingsWireData = mappingsWireData;
		}

		public void Update()
		{
			MappingsWireData.Description = Description;

			MappingsWireData.Source = Source;
			MappingsWireData.SourceInputActionMap = SourceInputActionMap;
			MappingsWireData.SourceInputAction = SourceInputAction;
			MappingsWireData.SourcePlayfieldItem = SourcePlayfieldItem;
			MappingsWireData.SourceConstant = SourceConstant;
			MappingsWireData.SourceDevice = SourceDevice;
			MappingsWireData.SourceDeviceItem = SourceDeviceItem;

			MappingsWireData.Destination = Destination;
			MappingsWireData.DestinationPlayfieldItem = DestinationPlayfieldItem;
			MappingsWireData.DestinationDevice = DestinationDevice;
			MappingsWireData.DestinationDeviceItem = DestinationDeviceItem;

			MappingsWireData.PulseDelay = PulseDelay;
		}
	}
}
