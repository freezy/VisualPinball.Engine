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
	[PackAs("MusicRequester")]
	public class MusicRequester : MonoBehaviour, IPackable
	{
		#region Data

		public MusicAsset MusicAsset;
		public SoundPriority Priority = SoundPriority.Medium;
		public float Volume = 1f;

		#endregion

		#region Packaging

		public byte[] Pack() => PackageApi.Packer.Empty;

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files)
			=> MusicRequesterPackable.Pack(this, files);

		public void Unpack(byte[] bytes) { }

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files)
			=> MusicRequesterPackable.Unpack(data, this, files);

		#endregion

		private int requestId;

		private MusicCoordinator _coordinator;

		private void OnEnable()
		{
			var request = new MusicRequest(MusicAsset, Priority, Volume);
			_coordinator = GetComponentInParent<MusicCoordinator>();
			_coordinator.AddRequest(request, out requestId);
		}

		private void OnDisable()
		{
			_coordinator.RemoveRequest(requestId);
		}
	}
}
