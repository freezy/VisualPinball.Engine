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

using System;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Used to request from <c>MusicCoordinator</c> to play a <c>MusicAsset</c>. Supports sorting
	/// to decide which request is played first.
	/// </summary>
	public struct MusicRequest : IComparable<MusicRequest>
	{
		public MusicRequest(
			MusicAsset musicAsset,
			SoundPriority priority = SoundPriority.Medium,
			float volume = 1f
		)
		{
			MusicAsset = musicAsset;
			Priority = priority;
			Index = -1;
			Volume = volume;
		}

		public readonly MusicAsset MusicAsset;
		public readonly SoundPriority Priority;
		public readonly float Volume;

		/// <summary>
		/// The <c>MusicCoordinator</c> sets the <c>Index</c> of the <c>n</c>th request it receives
		/// to <c>n</c>. This allows sorting requests by receive order and uniquely identifies each
		/// request so it can be removed later using its <c>Index</c>.
		/// </summary>
		public int Index { get; set; }

		// Used to sort the request stack to determine which request to play
		public readonly int CompareTo(MusicRequest other)
		{
			if (Priority != other.Priority)
				return other.Priority.CompareTo(Priority);
			// If priority is the same, favor newer requests
			return other.Index.CompareTo(Index);
		}
	}
}
