// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
	public class WireListData : IManagerListData, IDeviceListData<IGamelogicEngineDeviceItem>
	{
		public string Id;

		[ManagerListColumn(Order = 0, HeaderName = "Description", Width = 150)]
		public string Name { get; private set; }

		[ManagerListColumn(Order = 1, HeaderName = "Source", Width = 150)]
		public SwitchSource Source;

		[ManagerListColumn(Order = 2, HeaderName = "Source Element", Width = 270)]
		public string SourceInputActionMap;
		public string SourceInputAction;
		public SwitchConstant SourceConstant;
		public ISwitchDeviceComponent SourceDevice;
		public string SourceDeviceItem;

		[ManagerListColumn(Order = 3, HeaderName = "Destination Element", Width = 270)]
		public IWireableComponent DestinationDevice;
		public string DestinationDeviceItem;

		[ManagerListColumn(Order = 4, HeaderName = "Dynamic", Width = 100)]
		public bool IsDynamic;

		[ManagerListColumn(Order = 5, HeaderName = "Pulse Delay", Width = 100)]
		public int PulseDelay;

		public string Description { get => Name; set => Name = value; }

		public readonly WireMapping WireMapping;

		public IDeviceComponent<IGamelogicEngineDeviceItem> DeviceComponent => DestinationDevice;
		public string DeviceItem { get => DestinationDeviceItem; set => DestinationDeviceItem = value; }

		public WireListData(WireMapping wireMapping)
		{
			Id = wireMapping.Id;
			Description = wireMapping.Description;
			Source = wireMapping.Source;
			SourceInputActionMap = wireMapping.SourceInputActionMap;
			SourceInputAction = wireMapping.SourceInputAction;
			SourceConstant = wireMapping.SourceConstant;
			SourceDevice = wireMapping.SourceDevice;
			SourceDeviceItem = wireMapping.SourceDeviceItem;
			IsDynamic = wireMapping.IsDynamic;

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
			WireMapping.IsDynamic = IsDynamic;

			WireMapping.DestinationDevice = DestinationDevice;
			WireMapping.DestinationDeviceItem = DestinationDeviceItem;

			WireMapping.PulseDelay = PulseDelay;
		}

		public void ClearDevice()
		{
			DestinationDevice = null;
			DestinationDeviceItem = null;
			Update();
		}
	}
}
