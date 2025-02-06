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

using MemoryPack;
using VisualPinball.Unity.Editor.Packaging;

namespace VisualPinball.Unity
{
	[MemoryPackable]
	public partial struct DropTargetPackable
	{
		public static byte[] Pack(DropTargetComponent _) => PackageApi.Packer.Pack(new DropTargetPackable());

		public static void Unpack(byte[] bytes, DropTargetComponent comp)
		{
			// no data
		}
	}

	[MemoryPackable]
	public partial struct DropTargetColliderPackable
	{
		public bool IsLegacy;
		public float Threshold;
		public bool UseHitEvent;

		public static byte[] Pack(DropTargetColliderComponent comp)
		{
			return PackageApi.Packer.Pack(new DropTargetColliderPackable {
				IsLegacy = comp.IsLegacy,
				Threshold = comp.Threshold,
				UseHitEvent = comp.UseHitEvent,
			});
		}

		public static void Unpack(byte[] bytes, DropTargetColliderComponent comp)
		{
			var data = PackageApi.Packer.Unpack<DropTargetColliderPackable>(bytes);
			comp.IsLegacy = data.IsLegacy;
			comp.Threshold = data.Threshold;
			comp.UseHitEvent = data.UseHitEvent;
		}
	}
}
