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

using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	public struct SegmentDisplayPackable
	{
		public string Id;
		public float AspectRatio;
		public int NumChars;
		public int NumSegments;
		public Color LitColor;
		public Color UnlitColor;
		public float SkewAngle;
		public float SegmentWeight;
		public float HorizontalMiddle;
		public float2 Padding;
		public float2 SeparatorPos;
		public int SeparatorType;
		public bool SeparatorEveryThreeOnly;
		public float Emission;

		public static byte[] Pack(SegmentDisplayComponent comp)
		{
			return PackageApi.Packer.Pack(new SegmentDisplayPackable {
				Id = comp.Id,
				AspectRatio = comp.AspectRatio,
				NumChars = comp.NumChars,
				NumSegments = comp.NumSegments,
				LitColor = comp.LitColor,
				UnlitColor = comp.UnlitColor,
				SkewAngle = comp.SkewAngle,
				SegmentWeight = comp.SegmentWeight,
				HorizontalMiddle = comp.HorizontalMiddle,
				Padding = comp.Padding,
				SeparatorPos = comp.SeparatorPos,
				SeparatorType = comp.SeparatorType,
				SeparatorEveryThreeOnly = comp.SeparatorEveryThreeOnly,
				Emission = comp.Emission,
			});
		}

		public static void Unpack(byte[] bytes, SegmentDisplayComponent comp)
		{
			var data = PackageApi.Packer.Unpack<SegmentDisplayPackable>(bytes);
			comp.Id = data.Id;
			comp.AspectRatio = data.AspectRatio;
			comp.NumChars = data.NumChars;
			comp.NumSegments = data.NumSegments;
			comp.LitColor = data.LitColor;
			comp.UnlitColor = data.UnlitColor;
			comp.SkewAngle = data.SkewAngle;
			comp.SegmentWeight = data.SegmentWeight;
			comp.HorizontalMiddle = data.HorizontalMiddle;
			comp.Padding = data.Padding;
			comp.SeparatorPos = data.SeparatorPos;
			comp.SeparatorType = data.SeparatorType;
			comp.SeparatorEveryThreeOnly = data.SeparatorEveryThreeOnly;
			comp.Emission = data.Emission;
		}
	}
}
