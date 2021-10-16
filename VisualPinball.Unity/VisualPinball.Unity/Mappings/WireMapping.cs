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

using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace VisualPinball.Unity
{
	[Serializable]
	public class WireMapping
	{
		public string Id;

		public string Description = string.Empty;

		/* Source */
		public SwitchSource Source = SwitchSource.Playfield;

		public string SourceInputActionMap = string.Empty;

		public string SourceInputAction = string.Empty;

		public SwitchConstant SourceConstant;

		[SerializeReference]
		public MonoBehaviour _sourceDevice;
		public ISwitchDeviceComponent SourceDevice { get => _sourceDevice as ISwitchDeviceComponent; set => _sourceDevice = value as MonoBehaviour; }

		public string SourceDeviceItem = string.Empty;

		/* Destination */
		[SerializeReference]
		public MonoBehaviour _destinationDevice;
		public IWireableComponent DestinationDevice { get => _destinationDevice as IWireableComponent; set => _destinationDevice = value as MonoBehaviour; }

		public string DestinationDeviceItem = string.Empty;

		public int PulseDelay = 250;

		public bool IsDynamic;

		public WireMapping()
		{
		}

		[ExcludeFromCodeCoverage]
		public WireMapping(string description, SwitchMapping switchMapping, CoilMapping coilMapping) : this(description, switchMapping)
		{
			DestinationDevice = coilMapping.Device;
			DestinationDeviceItem = coilMapping.DeviceItem;
		}

		[ExcludeFromCodeCoverage]
		public WireMapping(string description, SwitchMapping switchMapping, LampMapping lampMapping) : this(description, switchMapping)
		{
			DestinationDevice = lampMapping.Device;
			DestinationDeviceItem = lampMapping.DeviceItem;
		}

		[ExcludeFromCodeCoverage]
		private WireMapping(string description, SwitchMapping switchMapping)
		{
			Description = description;
			Source = switchMapping.Source;
			SourceDevice = switchMapping.Device;
			SourceDeviceItem = switchMapping.DeviceItem;
			SourceInputAction = switchMapping.InputAction;
			SourceInputActionMap = switchMapping.InputActionMap;
		}

		public WireMapping WithId()
		{
			Id = $"wire_{Guid.NewGuid().ToString().Substring(0, 8)}";
			return this;
		}

		public WireMapping Dynamic()
		{
			IsDynamic = true;
			return this;
		}

		[ExcludeFromCodeCoverage]
		public string Src { get {
			switch (Source) {
				case SwitchSource.Playfield: return $"{SourceDevice?.name}:{SourceDeviceItem}";
				case SwitchSource.InputSystem: return $"{SourceInputActionMap}:{SourceInputAction}";
				case SwitchSource.Constant: return "<constant value>";
				default: return "<unknown source>";
			}
		}}

		[ExcludeFromCodeCoverage]
		public string Dst => $"{DestinationDevice?.name}:{DestinationDeviceItem}";

	}
}
