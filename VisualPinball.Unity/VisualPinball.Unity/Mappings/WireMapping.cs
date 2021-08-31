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

// ReSharper disable InconsistentNaming

using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class WireMapping
	{
		public string Description = string.Empty;

		/* Source */
		public ESwitchSource Source = ESwitchSource.Playfield;

		public string SourceInputActionMap = string.Empty;

		public string SourceInputAction = string.Empty;

		public int SourceConstant;

		[SerializeReference]
		public MonoBehaviour _sourceDevice;
		public ISwitchDeviceAuthoring SourceDevice { get => _sourceDevice as ISwitchDeviceAuthoring; set => _sourceDevice = value as MonoBehaviour; }

		public string SourceDeviceId = string.Empty;

		/* Destination */
		[SerializeReference]
		public MonoBehaviour _destinationDevice;
		public ICoilDeviceAuthoring DestinationDevice { get => _destinationDevice as ICoilDeviceAuthoring; set => _destinationDevice = value as MonoBehaviour; }

		public string DestinationDeviceId = string.Empty;

		public int PulseDelay = 250;

		public WireMapping()
		{
		}

		[ExcludeFromCodeCoverage]
		public WireMapping(string description, SwitchMapping switchMapping, CoilMapping coilMapping)
		{
			Description = description;
			Source = switchMapping.Source;
			SourceDevice = switchMapping.Device;
			SourceDeviceId = switchMapping.DeviceSwitchId;
			SourceInputAction = switchMapping.InputAction;
			SourceInputActionMap = switchMapping.InputActionMap;
			DestinationDevice = coilMapping.Device;
			DestinationDeviceId = coilMapping.DeviceCoilId;
		}

		[ExcludeFromCodeCoverage]
		public string Src { get {
			switch (Source) {
				case ESwitchSource.Playfield: return $"{SourceDevice?.name}:{SourceDeviceId}";
				case ESwitchSource.InputSystem: return $"{SourceInputActionMap}:{SourceInputAction}";
				case ESwitchSource.Constant: return "<constant value>";
				default: return "<unknown source>";
			}
		}}

		[ExcludeFromCodeCoverage]
		public string Dst => $"{DestinationDevice?.name}:{DestinationDeviceId}";

	}
}
