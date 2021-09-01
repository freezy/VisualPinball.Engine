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
	public class WireListData : IManagerListData
	{
		[ManagerListColumn(Order = 0, HeaderName = "Description", Width = 150)]
		public string Name => Description;

		[ManagerListColumn(Order = 1, HeaderName = "Source", Width = 150)]
		public SwitchSource Source;

		[ManagerListColumn(Order = 2, HeaderName = "Source Element", Width = 270)]
		public string SourceElement;

		[ManagerListColumn(Order = 3, HeaderName = "Destination Element", Width = 270)]
		public string DestinationElement;

		[ManagerListColumn(Order = 4, HeaderName = "Pulse Delay", Width = 100)]
		public int PulseDelay;

		public string Description;

		public string SourceInputActionMap;
		public string SourceInputAction;
		public SwitchConstant SourceConstant;
		public ISwitchDeviceAuthoring SourceDevice;
		public string SourceDeviceItem;

		public ICoilDeviceAuthoring DestinationDevice;
		public string DestinationDeviceItem;

		public readonly WireMapping WireMapping;

		public WireListData(WireMapping wireMapping) {
			Description = wireMapping.Description;

			Source = wireMapping.Source;
			SourceInputActionMap = wireMapping.SourceInputActionMap;
			SourceInputAction = wireMapping.SourceInputAction;
			SourceConstant = wireMapping.SourceConstant;
			SourceDevice = wireMapping.SourceDevice;
			SourceDeviceItem = wireMapping.SourceDeviceItem;

			DestinationDevice = wireMapping.DestinationDevice;
			DestinationDeviceItem = wireMapping.DestinationDeviceItem;

			PulseDelay = wireMapping.PulseDelay;

			WireMapping = wireMapping;
		}

		public void Update()
		{
			WireMapping.Description = Description;

			WireMapping.Source = Source;
			WireMapping.SourceInputActionMap = SourceInputActionMap;
			WireMapping.SourceInputAction = SourceInputAction;
			WireMapping.SourceConstant = SourceConstant;
			WireMapping.SourceDevice = SourceDevice;
			WireMapping.SourceDeviceItem = SourceDeviceItem;

			WireMapping.DestinationDevice = DestinationDevice;
			WireMapping.DestinationDeviceItem = DestinationDeviceItem;

			WireMapping.PulseDelay = PulseDelay;
		}
	}
}
