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
	public struct RampPackable
	{
		public int Type;
		public float HeightBottom;
		public float HeightTop;
		public int ImageAlignment;
		public float LeftWallHeightVisible;
		public float RightWallHeightVisible;
		public float WidthBottom;
		public float WidthTop;
		public float WireDiameter;
		public float WireDistanceX;
		public float WireDistanceY;
		public IEnumerable<DragPointPackable> DragPoints;

		public static byte[] Pack(RampComponent comp)
		{
			return PackageApi.Packer.Pack(new RampPackable {
				Type = comp.Type,
				HeightBottom = comp.HeightBottom,
				HeightTop = comp.HeightTop,
				ImageAlignment = comp.ImageAlignment,
				LeftWallHeightVisible = comp.LeftWallHeightVisible,
				RightWallHeightVisible = comp.RightWallHeightVisible,
				WidthBottom = comp.WidthBottom,
				WidthTop = comp.WidthTop,
				WireDiameter = comp.WireDiameter,
				WireDistanceX = comp.WireDistanceX,
				WireDistanceY = comp.WireDistanceY,
				DragPoints = comp.DragPoints.Select(DragPointPackable.From)
			});
		}

		public static void Unpack(byte[] bytes, RampComponent comp)
		{
			var data = PackageApi.Packer.Unpack<RampPackable>(bytes);
			comp._type = data.Type;
			comp._heightBottom = data.HeightBottom;
			comp._heightTop = data.HeightTop;
			comp._imageAlignment = data.ImageAlignment;
			comp._leftWallHeightVisible = data.LeftWallHeightVisible;
			comp._rightWallHeightVisible = data.RightWallHeightVisible;
			comp._widthBottom = data.WidthBottom;
			comp._widthTop = data.WidthTop;
			comp._wireDiameter = data.WireDiameter;
			comp._wireDistanceX = data.WireDistanceX;
			comp._wireDistanceY = data.WireDistanceY;
			comp.DragPoints = data.DragPoints.Select(c => c.ToDragPoint()).ToArray();
		}
	}

	public struct RampColliderPackable
	{
		public bool IsMovable;
		public bool HitEvent;
		public float Threshold;
		public float LeftWallHeight;
		public float RightWallHeight;

		public static byte[] Pack(RampColliderComponent comp)
		{
			return PackageApi.Packer.Pack(new RampColliderPackable {
				IsMovable = comp._isKinematic,
				HitEvent = comp.HitEvent,
				Threshold = comp.Threshold,
				LeftWallHeight = comp.LeftWallHeight,
				RightWallHeight = comp.RightWallHeight
			});
		}

		public static void Unpack(byte[] bytes, RampColliderComponent comp)
		{
			var data = PackageApi.Packer.Unpack<RampColliderPackable>(bytes);
			comp._isKinematic = data.IsMovable;
			comp.HitEvent = data.HitEvent;
			comp.Threshold = data.Threshold;
			comp.LeftWallHeight = data.LeftWallHeight;
			comp.RightWallHeight = data.RightWallHeight;
		}
	}
}
