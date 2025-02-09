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
	public struct GatePackable
	{
		public static byte[] Pack(GateComponent _) => PackageApi.Packer.Pack(new GatePackable());

		public static void Unpack(byte[] bytes, GateComponent comp)
		{
			// no data
		}
	}

	public struct GateColliderPackable
	{
		public bool IsMovable;
		public float AngleMax;
		public float AngleMin;
		public float ZLow;
		public float Distance;
		public float Damping;
		public float GravityFactor;
		public bool TwoWay;

		public static byte[] Pack(GateColliderComponent comp)
		{
			return PackageApi.Packer.Pack(new GateColliderPackable {
				IsMovable = comp._isKinematic,
				AngleMax = comp.AngleMax,
				AngleMin = comp.AngleMin,
				ZLow = comp.ZLow,
				Distance = comp.Distance,
				Damping = comp.Damping,
				GravityFactor = comp.GravityFactor,
				TwoWay = comp._twoWay
			});
		}

		public static void Unpack(byte[] bytes, GateColliderComponent comp)
		{
			var data = PackageApi.Packer.Unpack<GateColliderPackable>(bytes);
			comp._isKinematic = data.IsMovable;
			comp.AngleMax = data.AngleMax;
			comp.AngleMin = data.AngleMin;
			comp.ZLow = data.ZLow;
			comp.Distance = data.Distance;
			comp.Damping = data.Damping;
			comp.GravityFactor = data.GravityFactor;
			comp._twoWay = data.TwoWay;
		}
	}

	public struct GateLifterPackable
	{
		public float LiftedAngleDeg;
		public float AnimationSpeed;

		public static byte[] Pack(GateLifterComponent comp)
		{
			return PackageApi.Packer.Pack(new GateLifterPackable {
				LiftedAngleDeg = comp.LiftedAngleDeg,
				AnimationSpeed = comp.AnimationSpeed,
			});
		}

		public static void Unpack(byte[] bytes, GateLifterComponent comp)
		{
			var data = PackageApi.Packer.Unpack<GateLifterPackable>(bytes);
			comp.LiftedAngleDeg = data.LiftedAngleDeg;
			comp.AnimationSpeed = data.AnimationSpeed;
		}
	}
}
