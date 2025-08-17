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

// ReSharper disable InconsistentNaming

using UnityEngine;

namespace VisualPinball.Unity
{
	[PackAs("PlungerAnimation")]
	[DisallowMultipleComponent]
	public class PlungerAnimationComponent : AnimationComponent<float>, IPackable
	{
		private Quaternion _initialRotation;
		private SkinnedMeshRenderer[] _skinnedMeshRenderer;

		private void Start()
		{
			_skinnedMeshRenderer = GetComponentsInChildren<SkinnedMeshRenderer>();
		}

		protected override void OnAnimationValueChanged(float value)
		{
			foreach (var skinnedMeshRenderer in _skinnedMeshRenderer) {
				skinnedMeshRenderer.SetBlendShapeWeight(0, value);
			}
		}

		#region Packaging

		public byte[] Pack() => PackageApi.Packer.Empty;

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => null;

		public void Unpack(byte[] bytes) { }

		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) { }

		#endregion
	}
}
