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
	public struct MetalWireGuidePackable
	{
		public float Height;
		public float Thickness;
		public float StandHeight;
		public PackableFloat3 Rotation;
		public float BendRadius;
		public IEnumerable<DragPointPackable> DragPoints;


		public static byte[] Pack(MetalWireGuideComponent comp)
		{
			return PackageApi.Packer.Pack(new MetalWireGuidePackable {
				Height = comp.Height,
				Thickness = comp.Thickness,
				StandHeight = comp.Standheight,
				Rotation = comp.Rotation,
				BendRadius = comp.Bendradius,
				DragPoints = comp.DragPoints.Select(DragPointPackable.From),
			});
		}

		public static void Unpack(byte[] bytes, MetalWireGuideComponent comp)
		{
			var data = PackageApi.Packer.Unpack<MetalWireGuidePackable>(bytes);
			comp._height = data.Height;
			comp._thickness = data.Thickness;
			comp._standheight = data.StandHeight;
			comp.Rotation = data.Rotation;
			comp._bendradius = data.BendRadius;
			comp.DragPoints = data.DragPoints.Select(c => c.ToDragPoint()).ToArray();
		}
	}

	public struct MetalWireGuideColliderPackable
	{
		public bool IsMovable;
		public bool HitEvent;
		public float HitHeight;

		public static byte[] Pack(MetalWireGuideColliderComponent comp)
		{
			return PackageApi.Packer.Pack(new MetalWireGuideColliderPackable {
				IsMovable = comp._isKinematic,
				HitEvent = comp.HitEvent,
				HitHeight = comp.HitHeight,
			});
		}

		public static void Unpack(byte[] bytes, MetalWireGuideColliderComponent comp)
		{
			var data = PackageApi.Packer.Unpack<MetalWireGuideColliderPackable>(bytes);
			comp._isKinematic = data.IsMovable;
			comp.HitEvent = data.HitEvent;
			comp.HitHeight = data.HitHeight;
		}
	}
}
