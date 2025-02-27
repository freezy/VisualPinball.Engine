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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity
{
	[PackAs("GateLifter")]
	[RequireComponent(typeof(GateComponent))]
	[AddComponentMenu("Pinball/Mechs/Gate Lifter")]
	public class GateLifterComponent : MonoBehaviour, ICoilDeviceComponent, IPackable
	{
		public const string LifterCoilItem = "lifter_coil";

		[Unit("degrees")]
		[Tooltip("How much to rotate the wire to the end position, in degrees.")]
		public float LiftedAngleDeg;

		[Tooltip("How fast to lift the wire to the end position.")]
		public float AnimationSpeed = 0.1f;

		#region Packaging

		public byte[] Pack() => GateLifterPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs lookup, PackagedFiles files) => null;

		public void Unpack(byte[] bytes) => GateLifterPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs lookup, PackagedFiles files) { }

		#endregion

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

		public GateLifterApi GateLifterApi { get; private set; }

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			GateLifterApi = new GateLifterApi(gameObject, player, physicsEngine);

			player.Register(GateLifterApi, this);
		}
	}
}
