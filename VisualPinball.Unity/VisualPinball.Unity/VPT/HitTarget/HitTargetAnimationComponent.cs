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
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity
{
	[PackAs("HitTargetAnimation")]
	[AddComponentMenu("Visual Pinball/Animation/Hit Target Animation")]
	[RequireComponent(typeof(HitTargetColliderComponent))]
	public class HitTargetAnimationComponent : AnimationComponent<HitTargetData, HitTargetComponent>, IPackable
	{
		#region Data

		[Min(0)]
		[Tooltip("How fast the hit target moves back when hit.")]
		public float Speed =  0.5f;

		[Range(-180f, 180f)]
		[Tooltip("Angle of how much the hit target rotates back when hit.")]
		public float MaxAngle = 13.0f;

		#endregion

		#region Packaging

		public byte[] Pack() => HitTargetAnimationPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs lookup, PackagedFiles files) => null;

		public void Unpack(byte[] bytes) => HitTargetAnimationPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs lookup, PackagedFiles files) { }

		#endregion
	}
}
