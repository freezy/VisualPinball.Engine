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
	public struct PrimitivePackable
	{
		public static byte[] Pack(PrimitiveComponent _) => PackageApi.Packer.Pack(new PrimitivePackable());

		public static void Unpack(byte[] bytes, PrimitiveComponent comp)
		{
			// no data
		}
	}

	public struct PrimitiveColliderPackable
	{
		public bool IsMovable;
		public bool HitEvent;
		public float Threshold;
		public float CollisionReductionFactor;

		public static byte[] Pack(PrimitiveColliderComponent comp)
		{
			return PackageApi.Packer.Pack(new PrimitiveColliderPackable {
				IsMovable = comp._isKinematic,
				HitEvent = comp.HitEvent,
				Threshold = comp.Threshold,
				CollisionReductionFactor = comp.CollisionReductionFactor,
			});
		}

		public static void Unpack(byte[] bytes, PrimitiveColliderComponent comp)
		{
			var data = PackageApi.Packer.Unpack<PrimitiveColliderPackable>(bytes);
			comp._isKinematic = data.IsMovable;
			comp.HitEvent = data.HitEvent;
			comp.Threshold = data.Threshold;
			comp.CollisionReductionFactor = data.CollisionReductionFactor;
		}
	}

	public struct PrimitiveMeshPackable
	{
		public bool UseLegacyMesh;
		public int Sides;

		public static byte[] Pack(PrimitiveMeshComponent comp)
		{
			return PackageApi.Packer.Pack(new PrimitiveMeshPackable {
				UseLegacyMesh = comp.UseLegacyMesh,
				Sides = comp.Sides,
			});
		}

		public static void Unpack(byte[] bytes, PrimitiveMeshComponent comp)
		{
			var data = PackageApi.Packer.Unpack<PrimitiveMeshPackable>(bytes);
			comp.UseLegacyMesh = data.UseLegacyMesh;
			comp.Sides = data.Sides;
		}
	}
}
