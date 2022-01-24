// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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
using System.ComponentModel;
using System;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Mechs/Drop Target Bank")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/drop-target-banks.html")]
	public class DropTargetBankComponent : MonoBehaviour, ICoilDeviceComponent
	{
		public const string ResetCoilItem = "reset_coil";

		[ToolboxItem("The number of the drop targets. See documentation of a description of each type.")]
		public int BankSize = 1;

		[SerializeField]
		[Tooltip("Drop Targets")]
		public DropTargetComponent[] DropTargets = Array.Empty<DropTargetComponent>();

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil(ResetCoilItem) {
				Description = "Reset Coil"
			}
		};

		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;

		public static GameObject LoadPrefab() => Resources.Load<GameObject>("Prefabs/DropTargetBank");

		#region Runtime

		private void Awake()
		{
			GetComponentInParent<Player>().RegisterDropTargetBankComponent(this);
		}

		#endregion
	}
}
