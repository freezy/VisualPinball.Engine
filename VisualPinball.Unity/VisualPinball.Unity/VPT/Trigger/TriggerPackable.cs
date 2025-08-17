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

using System.Collections.Generic;
using System.Linq;

namespace VisualPinball.Unity
{
	public struct TriggerPackable
	{
		public IEnumerable<DragPointPackable> DragPoints;

		public static byte[] Pack(TriggerComponent comp)
		{
			return PackageApi.Packer.Pack(new TriggerPackable {
				DragPoints = comp.DragPoints.Select(DragPointPackable.From)
			});
		}

		public static void Unpack(byte[] bytes, TriggerComponent comp)
		{
			var data = PackageApi.Packer.Unpack<TriggerPackable>(bytes);
			comp.DragPoints = data.DragPoints.Select(c => c.ToDragPoint()).ToArray();
		}
	}

	public struct TriggerColliderPackable
	{
		public bool IsMovable;
		public float HitHeight;
		public float HitCircleRadius;

		public static byte[] Pack(TriggerColliderComponent comp)
		{
			return PackageApi.Packer.Pack(new TriggerColliderPackable {
				IsMovable = comp._isKinematic,
				HitHeight = comp.HitHeight,
				HitCircleRadius = comp.HitCircleRadius,
			});
		}

		public static void Unpack(byte[] bytes, TriggerColliderComponent comp)
		{
			var data = PackageApi.Packer.Unpack<TriggerColliderPackable>(bytes);
			comp._isKinematic = data.IsMovable;
			comp.HitHeight = data.HitHeight;
			comp.HitCircleRadius = data.HitCircleRadius;
		}
	}

	public struct TriggerMeshPackable
	{
		public int Shape;
		public float WireThickness;

		public static byte[] Pack(TriggerMeshComponent comp)
		{
			return PackageApi.Packer.Pack(new TriggerMeshPackable {
				Shape = comp.Shape,
				WireThickness = comp.WireThickness,
			});
		}

		public static void Unpack(byte[] bytes, TriggerMeshComponent comp)
		{
			var data = PackageApi.Packer.Unpack<TriggerMeshPackable>(bytes);
			comp.Shape = data.Shape;
			comp.WireThickness = data.WireThickness;
		}
	}

	public struct TriggerAnimationPackable
	{
		public float AnimSpeed;

		public static byte[] Pack(TriggerAnimationComponentLegacy comp)
		{
			return PackageApi.Packer.Pack(new TriggerAnimationPackable {
				AnimSpeed = comp.AnimSpeed
			});
		}

		public static void Unpack(byte[] bytes, TriggerAnimationComponentLegacy comp)
		{
			var data = PackageApi.Packer.Unpack<TriggerAnimationPackable>(bytes);
			comp.AnimSpeed = data.AnimSpeed;
		}
	}
}
