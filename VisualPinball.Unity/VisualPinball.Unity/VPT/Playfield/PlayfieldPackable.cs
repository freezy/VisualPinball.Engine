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
	public struct PlayfieldPackable
	{
		public float GlassHeight;
		public float Left;
		public float Right;
		public float Top;
		public float Bottom;
		public float AngleTiltMax;
		public float AngleTiltMin;
		public float RenderSlope;
		public int PlayfieldDetailLevel;

		public static byte[] Pack(PlayfieldComponent comp)
		{
			return PackageApi.Packer.Pack(new PlayfieldPackable {
				GlassHeight = comp.GlassHeight,
				Left = comp.Left,
				Right = comp.Right,
				Top = comp.Top,
				Bottom = comp.Bottom,
				AngleTiltMax = comp.AngleTiltMax,
				AngleTiltMin = comp.AngleTiltMin,
				RenderSlope = comp.RenderSlope,
				PlayfieldDetailLevel = comp.PlayfieldDetailLevel,
			});
		}

		public static void Unpack(byte[] bytes, PlayfieldComponent comp)
		{
			var data = PackageApi.Packer.Unpack<PlayfieldPackable>(bytes);
			comp.GlassHeight = data.GlassHeight;
			comp.Left = data.Left;
			comp.Right = data.Right;
			comp.Top = data.Top;
			comp.Bottom = data.Bottom;
			comp.AngleTiltMax = data.AngleTiltMax;
			comp.AngleTiltMin = data.AngleTiltMin;
			comp.RenderSlope = data.RenderSlope;
			comp.PlayfieldDetailLevel = data.PlayfieldDetailLevel;
		}
	}

	public struct PlayfieldColliderPackable
	{
		public float Gravity;
		public float DefaultScatter;
		public bool CollideWithBounds;

		public static byte[] Pack(PlayfieldColliderComponent comp)
		{
			return PackageApi.Packer.Pack(new PlayfieldColliderPackable {
				Gravity = comp.Gravity,
				DefaultScatter = comp.DefaultScatter,
				CollideWithBounds = comp.CollideWithBounds,
			});
		}

		public static void Unpack(byte[] bytes, PlayfieldColliderComponent comp)
		{
			var data = PackageApi.Packer.Unpack<PlayfieldColliderPackable>(bytes);
			comp.Gravity = data.Gravity;
			comp.DefaultScatter = data.DefaultScatter;
			comp.CollideWithBounds = data.CollideWithBounds;
		}
	}
}
