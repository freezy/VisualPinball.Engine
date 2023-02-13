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
using System.Collections.Generic;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEngine;

namespace VisualPinball.Engine.VPT
{
	//Implemented in components relating to sounds, including the Mechanical Sounds component.
	public interface ISoundEmitter
	{
		SoundTrigger[] AvailableTriggers { get;}
		VolumeEmitter[] GetVolumeEmitters(SoundTrigger trigger);
		event EventHandler<SoundEventArgs> OnSound;
	}

	public struct SoundTrigger
	{
		public string Id;
		public string Name;

	}

	//The emitter for the sound trigger. Determines how the volume will be calculated. 
	//Example: The "Fixed" volume emitter would be an emitter for the "Coil On" trigger. Same volume as configured by the sound asset.
	//Example: The "Ball Velocity" volume emitter would be an emitter for the "Ball Collision" trigger. Volume depends on the ball velocity upon collision.
	public struct VolumeEmitter
	{
		public string Id;
		public string Name;
	}

	public struct SoundEventArgs
	{
		public SoundTrigger Trigger;
		public float Volume;
	}
}
