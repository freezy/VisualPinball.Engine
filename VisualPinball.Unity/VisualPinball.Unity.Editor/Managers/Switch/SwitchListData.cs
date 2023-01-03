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

using System.Linq;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity.Editor
{
	public class SwitchListData : IManagerListData, IDeviceListData<GamelogicEngineSwitch>
	{
		[ManagerListColumn(Order = 0, HeaderName = "ID", Width = 150)]
		public string Name => Id;

		[ManagerListColumn(Order = 1, HeaderName = "NC", Width = 30)]
		public bool NormallyClosed;

		[ManagerListColumn(Order = 2, HeaderName = "Description", Width = 150)]
		public string Description { get; set; }

		[ManagerListColumn(Order = 3, HeaderName = "Source", Width = 150)]
		public SwitchSource Source;

		[ManagerListColumn(Order = 4, HeaderName = "Element", Width = 270)]
		public string Element;

		[ManagerListColumn(Order = 5, HeaderName = "Pulse Delay", Width = 100)]
		public int PulseDelay;

		public string Id;
		public string InputActionMap;
		public string InputAction;
		public SwitchConstant Constant;
		public ISwitchDeviceComponent Device;
		public string DeviceItem { get; set; }

		public readonly SwitchMapping SwitchMapping;

		public IDeviceComponent<GamelogicEngineSwitch> DeviceComponent => Device;

		public SwitchListData(SwitchMapping switchMapping) {
			Id = switchMapping.Id;
			NormallyClosed = switchMapping.IsNormallyClosed;
			Description = switchMapping.Description;
			Source = switchMapping.Source;
			InputActionMap = switchMapping.InputActionMap;
			InputAction = switchMapping.InputAction;
			Constant = switchMapping.Constant;
			Device = switchMapping.Device;
			if (string.IsNullOrEmpty(switchMapping.DeviceItem) && Device != null && Device.AvailableSwitches.Count() == 1) {
				DeviceItem = Device.AvailableSwitches.First().Id;

			} else {
				DeviceItem = switchMapping.DeviceItem;
			}

			PulseDelay = switchMapping.PulseDelay;

			SwitchMapping = switchMapping;
		}

		public void Update()
		{
			SwitchMapping.Id = Id;
			SwitchMapping.IsNormallyClosed = NormallyClosed;
			SwitchMapping.Description = Description;
			SwitchMapping.Source = Source;
			SwitchMapping.InputActionMap = InputActionMap;
			SwitchMapping.InputAction = InputAction;
			SwitchMapping.Constant = Constant;
			SwitchMapping.Device = Device;
			SwitchMapping.DeviceItem = DeviceItem;
			SwitchMapping.PulseDelay = PulseDelay;
		}

		public void ClearDevice()
		{
			Device = null;
			DeviceItem = null;
			Update();
		}
	}
}
