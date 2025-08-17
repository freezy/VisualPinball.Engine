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

using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	[PackAs("GateWireAnimation")]
	public class GateWireAnimationComponent : AnimationComponent<float>, IPackable
	{
		public Vector3 RotationVector = Vector3.left;
		private Quaternion _initialRotation;

		private new void Awake()
		{
			_initialRotation = transform.localRotation;
			base.Awake();
		}

		protected override void OnAnimationValueChanged(float value)
		{
			var axis = RotationVector.normalized;
			var rotation = Quaternion.AngleAxis(math.degrees(value), axis);
			transform.localRotation = _initialRotation * rotation;
		}

		#region Packaging

		public byte[] Pack() => GateWireAnimationPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => null;

		public void Unpack(byte[] bytes) => GateWireAnimationPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) { }

		#endregion
	}
}
