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

using System;

namespace VisualPinball.Unity
{
	/// <summary>
	/// An interface for item components that emit mechanical sounds.
	/// </summary>
	public interface ISoundEmitter
	{
		/// <summary>
		/// A list of triggers that can be linked to emitting a sound.
		/// </summary>
		SoundTrigger[] AvailableTriggers { get; }
		
		/// <summary>
		/// The sound event, to which the <see cref="Player"/> subscribes to.
		/// </summary>
		event EventHandler<SoundEventArgs> OnSound;
	}

	public readonly struct SoundEventArgs
	{
		public readonly string TriggerId;
		public readonly float Volume;
		public readonly float Fade;

		public SoundEventArgs(string triggerId, float volume, float fade)
		{
			TriggerId = triggerId;
			Volume = volume;
			Fade = fade;
		}
	}
}
