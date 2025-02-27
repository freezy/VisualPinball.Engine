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

using System.Linq;
using UnityEngine;

namespace VisualPinball.Unity
{
	public struct RotatorReferencesPackable
	{
		public ReferencePackable TargetRef;
		public ReferencePackable[] RotateWithRefs;

		public static byte[] Pack(RotatorComponent comp, PackagedRefs refs)
		{
			return PackageApi.Packer.Pack(new RotatorReferencesPackable {
				TargetRef = refs.PackReference(comp._target),
				RotateWithRefs = refs.PackReferences(comp._rotateWith).ToArray(),
			});
		}

		public static void Unpack(byte[] bytes, RotatorComponent comp, PackagedRefs refs)
		{
			var data = PackageApi.Packer.Unpack<RotatorReferencesPackable>(bytes);
			comp._target = refs.Resolve<MonoBehaviour, IRotatableComponent>(data.TargetRef);
			comp._rotateWith = refs.Resolve<MonoBehaviour, IRotatableComponent>(data.RotateWithRefs).ToArray();
		}
	}

	public struct CannonRotatorPackable
	{
		public float Factor;

		public static byte[] Pack(CannonRotatorComponent comp)
		{
			return PackageApi.Packer.Pack(new CannonRotatorPackable {
				Factor = comp.Factor,
			});
		}

		public static void Unpack(byte[] bytes, CannonRotatorComponent comp)
		{
			var data = PackageApi.Packer.Unpack<CannonRotatorPackable>(bytes);
			comp.Factor = data.Factor;
		}
	}

	public struct CannonRotatorReferencesPackable
	{
		public ReferencePackable MechRef;

		public static byte[] Pack(CannonRotatorComponent comp, PackagedRefs refs)
		{
			return PackageApi.Packer.Pack(new CannonRotatorReferencesPackable {
				MechRef = refs.PackReference(comp._mech),
			});
		}

		public static void Unpack(byte[] bytes, CannonRotatorComponent comp, PackagedRefs refs)
		{
			var data = PackageApi.Packer.Unpack<CannonRotatorReferencesPackable>(bytes);
			comp._mech = refs.Resolve<MonoBehaviour, IMechHandler>(data.MechRef);
		}
	}

	public struct StepRotatorMechPackable
	{
		public int NumSteps;
		public MechMark[] Marks;

		public static byte[] Pack(StepRotatorMechComponent comp)
		{
			return PackageApi.Packer.Pack(new StepRotatorMechPackable {
				NumSteps = comp.NumSteps,
				Marks = comp.Marks,
			});
		}

		public static void Unpack(byte[] bytes, StepRotatorMechComponent comp)
		{
			var data = PackageApi.Packer.Unpack<StepRotatorMechPackable>(bytes);
			comp.NumSteps = data.NumSteps;
			comp.Marks = data.Marks;
		}
	}
}
