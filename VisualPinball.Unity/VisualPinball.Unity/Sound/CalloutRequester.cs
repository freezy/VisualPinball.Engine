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
	/// Requests a callout from the callout coordinator when enabled. Intended for testing.
	/// </summary>
	[PackAs("CalloutRequester")]
	public class CalloutRequester : MonoBehaviour, IPackable
	{
		#region Data

		public CalloutAsset CalloutAsset;
		public SoundPriority Priority = SoundPriority.Medium;
		public float MaxQueueTime = -1f;

		#endregion

		#region Packaging

		public byte[] Pack() => PackageApi.Packer.Empty;

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files)
			=> CalloutRequesterPackable.Pack(this, files);

		public void Unpack(byte[] bytes) { }

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files)
			=> CalloutRequesterPackable.Unpack(data, this, files);

		#endregion

		private CalloutCoordinator _coordinator;
		private int _requestId;

		private void OnEnable()
		{
			var request = new CalloutRequest(CalloutAsset, Priority, MaxQueueTime);
			_coordinator = GetComponentInParent<CalloutCoordinator>();
			_coordinator.EnqueueCallout(request, out _requestId);
		}

		private void OnDisable()
		{
			_coordinator.DequeueCallout(_requestId);
		}
	}
}
