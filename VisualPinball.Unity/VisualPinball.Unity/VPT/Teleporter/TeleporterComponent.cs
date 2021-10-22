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

using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using Logger = NLog.Logger;
using NLog;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Teleporter")]
	public class TeleporterComponent : MonoBehaviour, ICoilDeviceComponent
	{
		#region Data

		public KickerComponent PortalA;

		public KickerComponent PortalB;

		public float TimeMs;

		#endregion

		#region Overrides and Constants

		public const string CoilItem = "teleport";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		#endregion

		#region Wiring

		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil(CoilItem) {
				Description = "Teleport Ball"
			}
		};

		#endregion

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			if (player == null)
			{
				Logger.Error($"Cannot find player for cannon {name}.");
				return;
			}

			player.RegisterTeleporter(this);
		}
	}
}
