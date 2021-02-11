// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

// ReSharper disable StringLiteralTypo

using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity.Patcher
{
	[MetaMatch(TableName = "Terminator 2 (Williams 1991)", AuthorName = "NFOZZY")]
	public class Terminator2
	{
		[NameMatch("LeftRampCover")]
		[NameMatch("LeftRampSign")]
		[NameMatch("RightRampCover")]
		[NameMatch("RightRampSign")]
		[NameMatch("Plastics_LVL2")]
		[NameMatch("BumperCaps")]
		[NameMatch("RightRamp")]
		public void FixZPosition(Primitive primitive)
		{
			primitive.Data.Position.Z = 0;
		}
	}
}
