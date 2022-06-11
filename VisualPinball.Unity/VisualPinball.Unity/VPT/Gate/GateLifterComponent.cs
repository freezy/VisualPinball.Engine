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

// ReSharper disable InconsistentNaming

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Mechs/Gate Lifter")]
	public class GateLifterComponent : MonoBehaviour, ICoilDeviceComponent
	{
		public const string LifterCoilItem = "lifter_coil";

		[Tooltip("How much to rotate the wire to the end position, in degrees.")]
		public float LiftedAngleDeg;

		[Tooltip("How fast to lift the wire to the end position.")]
		public float AnimationSpeed = 0.1f;

		#region ICoilDeviceComponent

		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableCoils;
		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		public IEnumerable<GamelogicEngineCoil> AvailableCoils =>  new[] {
			new GamelogicEngineCoil(LifterCoilItem) {
				Description = "Lifter Coil"
			}
		};

		#endregion

		private void Awake()
		{
			GetComponentInParent<Player>().RegisterGateLifter(this);
			var wireComponent = GetComponentInChildren<GateWireAnimationComponent>();
		}
	}
}
