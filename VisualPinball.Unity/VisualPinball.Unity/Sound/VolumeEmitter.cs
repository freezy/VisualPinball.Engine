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

namespace VisualPinball.Unity
{
	/// <summary>
	/// Volume emitters are defined by <see cref="SoundTrigger"/>. They allow for
	/// certain triggers (i.e. collision) to pass an additional volume argument
	/// when playing the sound.
	/// </summary>
	public struct VolumeEmitter
	{
		public string Id;
		public string Name;

		public static VolumeEmitter Static => new() { Id = "static", Name = "Static" };
	}
}
