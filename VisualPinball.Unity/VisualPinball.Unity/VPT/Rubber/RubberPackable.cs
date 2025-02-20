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
	public struct RubberPackable
	{
		public int Thickness;
		public IEnumerable<DragPointPackable> DragPoints;

		public static byte[] Pack(RubberComponent comp)
		{
			return PackageApi.Packer.Pack(new RubberPackable {
				Thickness = comp.Thickness,
				DragPoints = comp.DragPoints.Select(DragPointPackable.From)
			});
		}

		public static void Unpack(byte[] bytes, RubberComponent comp)
		{
			var data = PackageApi.Packer.Unpack<RubberPackable>(bytes);
			comp._thickness = data.Thickness;
			comp.DragPoints = data.DragPoints.Select(c => c.ToDragPoint()).ToArray();
		}
	}

	public struct RubberColliderPackable
	{
		public bool IsMovable;
		public bool HitEvent;
		public float HitHeight;

		public static byte[] Pack(RubberColliderComponent comp)
		{
			return PackageApi.Packer.Pack(new RubberColliderPackable {
				IsMovable = comp._isKinematic,
				HitEvent = comp.HitEvent,
				HitHeight = comp.HitHeight,
			});
		}

		public static void Unpack(byte[] bytes, RubberColliderComponent comp)
		{
			var data = PackageApi.Packer.Unpack<RubberColliderPackable>(bytes);
			comp._isKinematic = data.IsMovable;
			comp.HitEvent = data.HitEvent;
			comp.HitHeight = data.HitHeight;
		}
	}
}
