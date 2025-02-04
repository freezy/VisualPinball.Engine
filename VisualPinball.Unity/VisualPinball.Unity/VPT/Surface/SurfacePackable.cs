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
using MemoryPack;
using VisualPinball.Unity.Editor.Packaging;

namespace VisualPinball.Unity
{
	[MemoryPackable]
	public partial struct SurfacePackable
	{
		public float HeightTop;
		public float HeightBottom;
		public IEnumerable<DragPointPackable> DragPoints;

		public static byte[] Pack(SurfaceComponent comp)
		{
			return PackageApi.Packer.Pack(new SurfacePackable {
				HeightTop = comp.HeightTop,
				HeightBottom = comp.HeightBottom,
				DragPoints = comp.DragPoints.Select(DragPointPackable.From)
			});
		}

		public static void Unpack(byte[] bytes, SurfaceComponent comp)
		{
			var data = PackageApi.Packer.Unpack<SurfacePackable>(bytes);
			comp.HeightTop = data.HeightTop;
			comp.HeightBottom = data.HeightBottom;
			comp.DragPoints = data.DragPoints.Select(c => c.ToDragPoint()).ToArray();
		}
	}

	[MemoryPackable]
	public partial struct SurfaceColliderPackable
	{
		public bool HitEvent;
		public float Threshold;
		public bool IsBottomSolid;
		public float SlingshotThreshold;
		public float SlingshotForce;

		public static byte[] Pack(SurfaceColliderComponent comp)
		{
			return PackageApi.Packer.Pack(new SurfaceColliderPackable {
				HitEvent = comp.HitEvent,
				Threshold = comp.Threshold,
				IsBottomSolid = comp.IsBottomSolid,
				SlingshotThreshold = comp.SlingshotThreshold,
				SlingshotForce = comp.SlingshotForce
			});
		}

		public static void Unpack(byte[] bytes, SurfaceColliderComponent comp)
		{
			var data = PackageApi.Packer.Unpack<SurfaceColliderPackable>(bytes);
			comp.HitEvent = data.HitEvent;
			comp.Threshold = data.Threshold;
			comp.IsBottomSolid = data.IsBottomSolid;
			comp.SlingshotThreshold = data.SlingshotThreshold;
			comp.SlingshotForce = data.SlingshotForce;
		}
	}
}
