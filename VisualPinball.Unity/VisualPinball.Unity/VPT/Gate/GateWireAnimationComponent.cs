﻿// Visual Pinball Engine
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

using System;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity
{
	[PackAs("GateWireAnimation")]
	public class GateWireAnimationComponent : AnimationComponent<GateData, GateComponent>, IRotatableAnimationComponent, IPackable
	{
		public float min = float.MaxValue;
		public float max = float.MinValue;

		public void OnRotationUpdated(float angleRad)
		{
			min = math.min(angleRad, min);
			max = math.max(angleRad, max);

			// Debug.Log($"Rotate: {angleRad} ({math.degrees(angleRad)}) [{math.degrees(min)} - {math.degrees(max)}]");

			transform.localRotation = quaternion.RotateX(-angleRad);
		}

		#region Packaging

		public byte[] Pack() => GateWireAnimationPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => null;

		public void Unpack(byte[] bytes) => GateWireAnimationPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) { }

		#endregion
	}
}
