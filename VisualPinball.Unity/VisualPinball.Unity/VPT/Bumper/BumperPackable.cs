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

namespace VisualPinball.Unity
{
	public struct BumperPackable
	{
		public float Radius;
		public bool IsHardwired;

		public static byte[] Pack(BumperComponent comp)
		{
			return PackageApi.Packer.Pack(new BumperPackable {
				Radius = comp.Radius,
				IsHardwired = comp.IsHardwired,
			});
		}

		public static void Unpack(byte[] bytes, BumperComponent comp)
		{
			var data = PackageApi.Packer.Unpack<BumperPackable>(bytes);
			comp.Radius = data.Radius;
			comp.IsHardwired = data.IsHardwired;
		}
	}

	public struct BumperColliderPackable
	{
		public bool IsMovable;
		public float Threshold;
		public float Force;
		public bool HitEvent;

		public static byte[] Pack(BumperColliderComponent comp)
		{
			return PackageApi.Packer.Pack(new BumperColliderPackable {
				IsMovable = comp._isKinematic,
				Threshold = comp.Threshold,
				Force = comp.Force,
				HitEvent = comp.HitEvent,
			});
		}

		public static void Unpack(byte[] bytes, BumperColliderComponent comp)
		{
			var data = PackageApi.Packer.Unpack<BumperColliderPackable>(bytes);
			comp._isKinematic = data.IsMovable;
			comp.Threshold = data.Threshold;
			comp.Force = data.Force;
			comp.HitEvent = data.HitEvent;
		}
	}

	public struct BumperRingAnimationPackable
	{
		public float RingSpeed;
		public float RingDropOffset;

		public static byte[] Pack(BumperRingAnimationComponentLegacy comp)
		{
			return PackageApi.Packer.Pack(new BumperRingAnimationPackable {
				RingSpeed = comp.RingSpeed,
				RingDropOffset = comp.RingDropOffset,
			});
		}

		public static void Unpack(byte[] bytes, BumperRingAnimationComponentLegacy comp)
		{
			var data = PackageApi.Packer.Unpack<BumperRingAnimationPackable>(bytes);
			comp.RingSpeed = data.RingSpeed;
			comp.RingDropOffset = data.RingDropOffset;
		}
	}

	public struct BumperSkirtAnimationPackable
	{
		public float Duration;

		public static byte[] Pack(BumperSkirtAnimationComponentLegacy comp)
		{
			return PackageApi.Packer.Pack(new BumperSkirtAnimationPackable {
				Duration = comp.duration,
			});
		}

		public static void Unpack(byte[] bytes, BumperSkirtAnimationComponentLegacy comp)
		{
			var data = PackageApi.Packer.Unpack<BumperSkirtAnimationPackable>(bytes);
			comp.duration = data.Duration;
		}
	}
}
