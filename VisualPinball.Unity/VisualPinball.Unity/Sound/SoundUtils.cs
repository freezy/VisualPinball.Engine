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

using UnityEngine;
using System.Threading;
using System.Threading.Tasks;

namespace VisualPinball.Unity
{
	public static class SoundUtils
	{
		public static async Task Fade(AudioSource audioSource, float fromVolume, float toVolume, float duration, CancellationToken ct)
		{
			float progress = 0f;
			while (progress < 1f && !ct.IsCancellationRequested) {
				audioSource.volume = Mathf.Lerp(fromVolume, toVolume, progress);
				progress += (1f / duration) * Time.deltaTime;
				await Task.Yield();
			}
		}
	}
}
