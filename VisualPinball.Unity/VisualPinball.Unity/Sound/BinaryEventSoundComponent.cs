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
using UnityEngine.Serialization;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Start and or stop a sound when an event occurs that represents a binary state change,
	/// such as a switch closing or a coil being energized.
	/// </summary>
	public abstract class BinaryEventSoundComponent<TEventSource, TEventArgs> : EventSoundComponent<TEventSource, TEventArgs> where TEventSource : class
	{
		[FormerlySerializedAs("_startWhen")]
		public StartWhen StartWhen = StartWhen.TurnedOn;

		[FormerlySerializedAs("_stopWhen")]
		public StopWhen StopWhen = StopWhen.Never;

		protected override async void OnEvent(object sender, TEventArgs e)
		{
			bool isEnabled = InterpretAsBinary(e);
			if ((isEnabled && StopWhen == StopWhen.TurnedOn) ||
			    (!isEnabled && StopWhen == StopWhen.TurnedOff))
			{
				Stop(allowFade: true);
			}

			if ((isEnabled && StartWhen == StartWhen.TurnedOn) ||
			    (!isEnabled && StartWhen == StartWhen.TurnedOff))
			{
				await Play();
			}
		}

		protected abstract bool InterpretAsBinary(TEventArgs e);

		public override bool SupportsLoopingSoundAssets() => StopWhen != StopWhen.Never;
	}

	public enum StartWhen { TurnedOn, TurnedOff }
	public enum StopWhen { Never, TurnedOn, TurnedOff }
}
