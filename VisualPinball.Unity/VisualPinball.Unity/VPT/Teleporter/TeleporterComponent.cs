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

using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using Logger = NLog.Logger;
using NLog;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Teleporter")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/teleporters.html")]
	public class TeleporterComponent : MonoBehaviour, ICoilDeviceComponent
	{
		#region Data

		[Tooltip("If set, the ball is automatically popped out of the destination kicker upon arrival.")]
		public bool EjectAfterTeleportation = true;

		[Min(0)]
		[Tooltip("The time in seconds between the ball arriving at the destination kicker and being popped out of the kicker.")]
		public float EjectDelay = 0.5f;

		[Tooltip("The kicker where the ball is teleported from.")]
		public KickerComponent FromKicker;

		[Tooltip("The kicker where the ball is teleported into, and which coil should be used to pop the ball out.")]
		[TypeRestriction(typeof(KickerComponent), PickerLabel = "Kickers", DeviceItem = nameof(ToKickerItem), DeviceType = typeof(ICoilDeviceComponent))]
		public KickerComponent ToKicker;
		public string ToKickerItem = string.Empty;

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

		#region Runtime

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

		#endregion
	}
}
