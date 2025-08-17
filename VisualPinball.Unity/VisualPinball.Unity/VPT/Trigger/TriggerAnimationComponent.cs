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
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity
{
	[PackAs("TriggerAnimation")]
	[AddComponentMenu("Pinball/Animation/Trigger Animation")]
	public class TriggerAnimationComponent : AnimationComponentLegacy<TriggerData, TriggerComponent>, IPackable
	{
		#region Data

		[Min(0)]
		[Tooltip("How quick the trigger moves down when the ball rolls over it.")]
		public float AnimSpeed = 1f;

		#endregion

		#region Packaging

		public byte[] Pack() => TriggerAnimationPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => null;

		public void Unpack(byte[] bytes) => TriggerAnimationPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) { }

		#endregion
	}
}
