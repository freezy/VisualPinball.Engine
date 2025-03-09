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

using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Requests music from the music coordinator while enabled. Intended for testing.
	/// </summary>
	public class MusicRequester : MonoBehaviour
	{
		[SerializeField]
		private MusicAsset _musicAsset;

		[SerializeField]
		private SoundPriority _priority = SoundPriority.Medium;

		[SerializeField]
		private float _volume = 1f;

		private int requestId;

		private MusicCoordinator _coordinator;

		private void OnEnable()
		{
			var request = new MusicRequest(_musicAsset, _priority, _volume);
			_coordinator = GetComponentInParent<MusicCoordinator>();
			_coordinator.AddRequest(request, out requestId);
		}

		private void OnDisable()
		{
			_coordinator.RemoveRequest(requestId);
		}
	}
}
