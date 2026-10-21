// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using System;
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
namespace VisualPinball.Unity
{
	[PackAs("Solenoid")]
	[AddComponentMenu("Pinball/Game Item/Solenoid")]
	public class SolenoidComponent : MonoBehaviour, ICoilDeviceComponent, IPackable
	{
		public const string CoilItem = "coil";
		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] { new GamelogicEngineCoil(CoilItem) { Description = "Solenoid" } };
		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;
		private SolenoidApi _api;
		IApiCoil ICoilDeviceComponent.CoilDevice(string id) => _api?.Coil(id);
		private void Awake() {
			var player = GetComponentInParent<Player>();
			if (player == null)
				return;
			_api = new SolenoidApi(player);
			player.Register(_api, this);
		}
		public byte[] Pack() => Array.Empty<byte>();
		public void Unpack(byte[] bytes) {
		}
		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => Array.Empty<byte>();
		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) {
		}
	}
	public class SolenoidApi : IApi, IApiCoilDevice, IApiWireDeviceDest
	{
		public event EventHandler Init;
		private readonly DeviceCoil _coil;
		public SolenoidApi(Player player) {
			_coil = new DeviceCoil(player);
		}
		public IApiCoil Coil(string id) => id == SolenoidComponent.CoilItem ? _coil : throw new ArgumentException("Unknown solenoid coil: " + id);
		IApiWireDest IApiWireDeviceDest.Wire(string id) => Coil(id);
		void IApi.OnInit(BallManager manager) => Init?.Invoke(this, EventArgs.Empty);
		void IApi.OnDestroy() {
		}
	}
}
