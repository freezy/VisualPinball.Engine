// Visual Pinball Engine
// Copyright (C) 2025 freezy and VPE Team
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
using UnityEngine;
using UnityEngine.Serialization;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Start and or stop a sound when a coil is energized or deenergized.
	/// </summary>
	[PackAs("SwitchSound")]
	[AddComponentMenu("Pinball/Sound/Switch Sound")]
	public class SwitchSoundComponent : BinaryEventSoundComponent<IApiSwitch, SwitchEventArgs>, IPackable
	{
		[FormerlySerializedAs("_switchName")]
		[HideInInspector]
		public string SwitchName;

		public override Type GetRequiredType() => typeof(ISwitchDeviceComponent);

		protected override bool TryFindEventSource(out IApiSwitch @switch)
		{
			@switch = null;
			var player = GetComponentInParent<Player>();
			if (player == null)
				return false;

			foreach (var component in GetComponents<ISwitchDeviceComponent>()) {
				@switch = player.Switch(component, SwitchName);
				if (@switch != null)
					return true;
			}
			return false;
		}

		protected override void Subscribe(IApiSwitch eventSource) => eventSource.Switch += OnEvent;

		protected override void Unsubscribe(IApiSwitch eventSource) =>
			eventSource.Switch -= OnEvent;

		protected override bool InterpretAsBinary(SwitchEventArgs e) => e.IsEnabled;

		#region Packaging

		// refs are handled by SoundComponent

		public new byte[] Pack() => SwitchSoundPackable.Pack(this);

		public new void Unpack(byte[] bytes) => SwitchSoundPackable.Unpack(bytes, this);

		#endregion
	}
}
