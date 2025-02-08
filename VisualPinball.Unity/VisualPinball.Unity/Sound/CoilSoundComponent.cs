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

using UnityEngine;
using System;
using UnityEngine.Serialization;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Start and or stop a sound when a coil is energized or deenergized.
	/// </summary>
	[PackAs("CoilSound")]
	[AddComponentMenu("Visual Pinball/Sound/Coil Sound")]
	public class CoilSoundComponent : BinaryEventSoundComponent<IApiCoil, NoIdCoilEventArgs>, IPackable
	{
		[FormerlySerializedAs("_coilName")]
		[HideInInspector]
		public string CoilName;

		public override Type GetRequiredType() => typeof(ICoilDeviceComponent);

		protected override bool TryFindEventSource(out IApiCoil coil)
		{
			coil = null;
			var player = GetComponentInParent<Player>();
			if (player == null) {
				return false;
			}

			foreach (var component in GetComponents<ICoilDeviceComponent>()) {
				coil = player.Coil(component, CoilName);
				if (coil != null) {
					return true;
				}
			}
			return false;
		}

		protected override void Subscribe(IApiCoil coil) => coil.CoilStatusChanged += OnEvent;

		protected override void Unsubscribe(IApiCoil coil) => coil.CoilStatusChanged -= OnEvent;

		protected override bool InterpretAsBinary(NoIdCoilEventArgs e) => e.IsEnergized;

		#region Packaging

		// refs are handled by SoundComponent

		public new byte[] Pack() => CoilSoundPackable.Pack(this);

		public new void Unpack(byte[] bytes) => CoilSoundPackable.Unpack(bytes, this);

		#endregion
	}
}
