// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using System.Collections.Generic;

namespace VisualPinball.Unity
{
	public sealed class SoundPlaybackContext
	{
		private readonly Dictionary<SoundEffectAsset, float> _starts = new();

		internal void Reset(SoundEffectAsset asset) => _starts.Remove(asset);

		public bool TryStart(SoundEffectAsset asset, float now, float cooldown) {
			if (_starts.TryGetValue(asset, out var last) && now >= last && now - last < cooldown)
				return false;
			_starts[asset] = now;
			return true;
		}
	}
}
