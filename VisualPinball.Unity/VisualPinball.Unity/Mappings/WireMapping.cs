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

// ReSharper disable InconsistentNaming

using System;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
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

		[JsonIgnore]
		[SerializeReference]
		public MonoBehaviour _sourceDevice;

		[JsonIgnore]
		public ISwitchDeviceComponent SourceDevice { get => _sourceDevice as ISwitchDeviceComponent; set => _sourceDevice = value as MonoBehaviour; }

		[JsonProperty]
		private string _sourceDeviceId { get; set; }

		public string SourceDeviceItem = string.Empty;

		/* Destination */
		[JsonIgnore]
		[SerializeReference]
		public MonoBehaviour _destinationDevice;

		[JsonIgnore]
		public IWireableComponent DestinationDevice { get => _destinationDevice as IWireableComponent; set => _destinationDevice = value as MonoBehaviour; }

		[JsonProperty]
		private string _destinationDeviceId { get; set; }

		public string DestinationDeviceItem = string.Empty;

		public int PulseDelay = 250;

		public bool IsDynamic;

		public WireMapping()
		{
		}

		public void SaveReferences(PackagedRefs refs)
		{
			_sourceDeviceId = refs.GetNodeId(_sourceDevice ? _sourceDevice.transform : null);
			_destinationDeviceId = refs.GetNodeId(_destinationDevice ? _destinationDevice.transform : null);
		}

		public void RestoreReferences(PackagedRefs refs)
		{
			var sourceNode = refs.GetNode(_sourceDeviceId);
			_sourceDevice = sourceNode ? sourceNode.GetComponent<ICoilDeviceComponent>() as MonoBehaviour : null;

			var destinationNode = refs.GetNode(_destinationDeviceId);
			_destinationDevice = destinationNode ? destinationNode.GetComponent<ICoilDeviceComponent>() as MonoBehaviour : null;
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
