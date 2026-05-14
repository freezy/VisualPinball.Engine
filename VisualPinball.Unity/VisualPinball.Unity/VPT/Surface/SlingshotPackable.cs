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

// ReSharper disable MemberCanBePrivate.Global

using UnityEngine;

namespace VisualPinball.Unity
{
	public struct SlingshotPackable
	{
		public float CoilArmStartAngle;
		public float CoilArmEndAngle;
		public int CoilArmRotationAxis;
		public float AnimationDuration;
		public AnimationCurve AnimationCurve;

		public static byte[] Pack(SlingshotComponent comp)
		{
			return PackageApi.Packer.Pack(new SlingshotPackable {
				CoilArmStartAngle = comp.CoilArmStartAngle,
				CoilArmEndAngle = comp.CoilArmEndAngle,
				CoilArmRotationAxis = (int)comp.CoilArmRotationAxis,
				AnimationDuration = comp.AnimationDuration,
				AnimationCurve = comp.AnimationCurve,
			});
		}

		public static void Unpack(byte[] bytes, SlingshotComponent comp)
		{
			var data = PackageApi.Packer.Unpack<SlingshotPackable>(bytes);
			comp.CoilArmStartAngle = data.CoilArmStartAngle;
			comp.CoilArmEndAngle = data.CoilArmEndAngle;
			comp.CoilArmRotationAxis = (Axis)data.CoilArmRotationAxis;
			comp.AnimationDuration = data.AnimationDuration;
			comp.AnimationCurve = data.AnimationCurve;
		}
	}

	public struct SlingshotReferencesPackable
	{
		public ReferencePackable SlingshotSurfaceRef;
		public ReferencePackable RubberOnRef;
		public ReferencePackable RubberOffRef;
		public string CoilArmPath;

		public static byte[] Pack(SlingshotComponent comp, Transform root, PackagedRefs refs)
		{
			return PackageApi.Packer.Pack(new SlingshotReferencesPackable {
				SlingshotSurfaceRef = refs.PackReference(comp.SlingshotSurface),
				RubberOnRef = refs.PackReference(comp.RubberOn),
				RubberOffRef = refs.PackReference(comp.RubberOff),
				CoilArmPath = comp.CoilArm ? comp.CoilArm.transform.GetPath(root, activeOnly: true) : null,
			});
		}

		public static void Unpack(byte[] bytes, SlingshotComponent comp, Transform root, PackagedRefs refs)
		{
			var data = PackageApi.Packer.Unpack<SlingshotReferencesPackable>(bytes);
			comp.SlingshotSurface = refs.Resolve<SurfaceColliderComponent>(data.SlingshotSurfaceRef);
			comp.RubberOn = refs.Resolve<RubberComponent>(data.RubberOnRef);
			comp.RubberOff = refs.Resolve<RubberComponent>(data.RubberOffRef);
			comp.CoilArm = string.IsNullOrEmpty(data.CoilArmPath) ? null : root.FindByPath(data.CoilArmPath)?.gameObject;
		}
	}
}
