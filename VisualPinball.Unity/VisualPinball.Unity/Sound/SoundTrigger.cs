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
	/// A sound trigger is describes how a mechanical sound is triggered.
	///
	/// During edit time, sound triggers are declared by game items so they
	/// can be linked to a <see cref="SoundAsset"/>. During runtime, they
	/// are used to identify which sound to play.
	/// </summary>
	public struct SoundTrigger
	{
		/// <summary>
		/// The ID of the trigger. When you change the ID of a trigger,
		/// all already associated triggers will be cleared.
		/// </summary>
		public string Id;
		
		/// <summary>
		/// Name of the trigger, used for display purposes only.
		/// </summary>
		public string Name;
	}
}
