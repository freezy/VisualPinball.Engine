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
	public struct PlungerPackable
	{
		public float Width;
		public float Height;

		public static byte[] Pack(PlungerComponent comp)
		{
			return PackageApi.Packer.Pack(new PlungerPackable {
				Width = comp.Width,
				Height = comp.Height,
			});
		}

		public static void Unpack(byte[] bytes, PlungerComponent comp)
		{
			var data = PackageApi.Packer.Unpack<PlungerPackable>(bytes);
			comp.Width = data.Width;
			comp.Height = data.Height;
		}
	}

	public struct PlungerColliderPackable
	{
		public bool IsMovable;
		public float SpeedPull;
		public float SpeedFire;
		public float Stroke;
		public float ScatterVelocity;
		public bool IsMechPlunger;
		public bool IsAutoPlunger;
		public float MechStrength;
		public float MomentumXfer;
		public float ParkPosition;

		public static byte[] Pack(PlungerColliderComponent comp)
		{
			return PackageApi.Packer.Pack(new PlungerColliderPackable {
				IsMovable = comp._isKinematic,
				SpeedPull = comp.SpeedPull,
				SpeedFire = comp.SpeedFire,
				Stroke = comp.Stroke,
				ScatterVelocity = comp.ScatterVelocity,
				IsMechPlunger = comp.IsMechPlunger,
				IsAutoPlunger = comp.IsAutoPlunger,
				MechStrength = comp.MechStrength,
				MomentumXfer = comp.MomentumXfer,
				ParkPosition = comp.ParkPosition,
			});
		}

		public static void Unpack(byte[] bytes, PlungerColliderComponent comp)
		{
			var data = PackageApi.Packer.Unpack<PlungerColliderPackable>(bytes);
			comp._isKinematic = data.IsMovable;
			comp.SpeedPull = data.SpeedPull;
			comp.SpeedFire = data.SpeedFire;
			comp.Stroke = data.Stroke;
			comp.ScatterVelocity = data.ScatterVelocity;
			comp.IsMechPlunger = data.IsMechPlunger;
			comp.IsAutoPlunger = data.IsAutoPlunger;
			comp.MechStrength = data.MechStrength;
			comp.MomentumXfer = data.MomentumXfer;
			comp.ParkPosition = data.ParkPosition;
		}
	}
}
