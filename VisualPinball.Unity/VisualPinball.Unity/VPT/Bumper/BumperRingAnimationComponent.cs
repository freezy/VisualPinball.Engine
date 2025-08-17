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
	[PackAs("BumperRingAnimation")]
	[AddComponentMenu("Pinball/Animation/Bumper Ring Animation")]
	public class BumperRingAnimationComponent : AnimationComponent<float>, IPackable
	{
		#region Data

		[Tooltip("How quick the ring moves down when the ball is hit.")]
		public float RingSpeed = 1.0f;

		[Tooltip("How low the ring drops. 0 = bottom")]
		public float RingDropOffset;

		#endregion

		#region Runtime

		private float _initialOffset;

		private void Start()
		{
			_initialOffset = transform.position.y;
		}

		protected override void OnAnimationValueChanged(float value)
		{
			var worldPos = transform.position;

			var limit = RingDropOffset + 0.5f; // dropped height scale here because this shouldn't be relevant in vpe.
			var localLimit = _initialOffset + limit;
			var localOffset = localLimit / limit * value;

			worldPos.y = _initialOffset + Physics.ScaleToWorld(localOffset);
			transform.position = worldPos;
		}

		#endregion

		#region Packaging

		public byte[] Pack() => BumperRingAnimationPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => null;

		public void Unpack(byte[] bytes) => BumperRingAnimationPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) { }

		#endregion
	}
}
