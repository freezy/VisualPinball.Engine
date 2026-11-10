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
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[Serializable]
	public class SwitchMapping
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public string Id = string.Empty;

		public bool IsNormallyClosed;

		public string Description = string.Empty;

		public SwitchSource Source = SwitchSource.Playfield;

		public string InputActionMap = string.Empty;

		public string InputAction = string.Empty;

		public SwitchConstant Constant;

		[JsonIgnore]
		public MonoBehaviour _device;

		[JsonIgnore]
		public ISwitchDeviceComponent Device { get => _device as ISwitchDeviceComponent; set => _device = value as MonoBehaviour; }

		[JsonProperty]
		private string _deviceId { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		private string _deviceType { get; set; }

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
		private int _deviceIndex { get; set; }

		public string DeviceItem = string.Empty;

		public int PulseDelay = 250;

		public void SaveReference(PackagedRefs refs)
		{
			_deviceId = refs.GetNodeId(_device ? _device.transform : null);
			_deviceType = null;
			_deviceIndex = 0;
			if (_deviceId == null) {
				return;
			}

			var type = _device.GetType();
			_deviceType = refs.GetName(type);
			if (_deviceType == null) {
				_deviceId = null;
				return;
			}
			// The package stores components in scene order within each concrete type.
			_deviceIndex = ComponentsOfType(_device, type).TakeWhile(component => component != _device).Count();
		}

		public void RestoreReference(PackagedRefs refs)
		{
			_device = null;
			var node = refs.GetNode(_deviceId);
			if (!node) {
				return;
			}

			if (!string.IsNullOrEmpty(_deviceType)) {
				if (_deviceIndex >= 0 && refs.TryGetType(_deviceType, out var type)
					&& typeof(ISwitchDeviceComponent).IsAssignableFrom(type)) {
					_device = ComponentsOfType(node, type).ElementAtOrDefault(_deviceIndex) as MonoBehaviour;
				}
				if (!_device) {
					Logger.Warn("Cannot restore switch mapping {SwitchId} on {Node}: missing {DeviceType} component at index {DeviceIndex}.",
						Id, node.name, _deviceType, _deviceIndex);
				}
				return;
			}

			// Older packages only name the node. Preserve a sole device even if its item needs repair.
			var devices = node.GetComponents<ISwitchDeviceComponent>();
			if (devices.Length == 1) {
				_device = devices[0] as MonoBehaviour;
				return;
			}

			var matches = devices.Where(OffersSwitch).Take(2).ToArray();
			if (matches.Length == 1) {
				_device = matches[0] as MonoBehaviour;
			} else {
				Logger.Warn("Cannot restore switch mapping {SwitchId} to {DeviceItem} on {Node}: {Reason}. Reassign the device and re-export the table.",
					Id, DeviceItem, node.name, matches.Length == 0 ? "no matching switch device" : "multiple matching switch devices");
			}
		}

		private bool OffersSwitch(ISwitchDeviceComponent device)
		{
			if (device is not MonoBehaviour component || !component) {
				return false;
			}
			try {
				return string.IsNullOrEmpty(DeviceItem) || device.AvailableSwitches.Any(item => item.Id == DeviceItem);
			} catch (Exception exception) {
				Logger.Warn(exception, "Cannot read available switches from {DeviceType} while restoring mapping {SwitchId}.",
					component.GetType().Name, Id);
				return false;
			}
		}

		private static IEnumerable<Component> ComponentsOfType(Component node, Type type)
			=> node.GetComponents(type).Where(component => component.GetType() == type);
	}
}
