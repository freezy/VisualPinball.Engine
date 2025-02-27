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

using UnityEngine;

namespace VisualPinball.Unity
{
	public struct DotMatrixDisplayPackable
	{
		public string Id;
		public float AspectRatio;
		public PackableColor LitColor;
		public PackableColor UnlitColor;
		public int Width;
		public int Height;
		public float Padding;
		public float Roundness;
		public float Emission;

		public static byte[] Pack(DotMatrixDisplayComponent comp)
		{
			return PackageApi.Packer.Pack(new DotMatrixDisplayPackable {
				Id = comp.Id,
				AspectRatio = comp.AspectRatio,
				LitColor = comp.LitColor,
				UnlitColor = comp.UnlitColor,
				Width = comp.Width,
				Height = comp.Height,
				Padding = comp.Padding,
				Roundness = comp.Roundness,
				Emission = comp.Emission,
			});
		}

		public static void Unpack(byte[] bytes, DotMatrixDisplayComponent comp)
		{
			var data = PackageApi.Packer.Unpack<DotMatrixDisplayPackable>(bytes);
			comp.Id = data.Id;
			comp.AspectRatio = data.AspectRatio;
			comp.LitColor = data.LitColor;
			comp.UnlitColor = data.UnlitColor;
			comp.Width = data.Width;
			comp.Height = data.Height;
			comp.Padding = data.Padding;
			comp.Roundness = data.Roundness;
			comp.Emission = data.Emission;
		}
	}
}
