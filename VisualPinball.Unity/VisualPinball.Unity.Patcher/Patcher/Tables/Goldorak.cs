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

// ReSharper disable StringLiteralTypo
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using UnityEngine;

namespace VisualPinball.Unity.Patcher
{
	[MetaMatch(TableName = "Goldorak", AuthorName = "Rom (Future PinBall) Javier VPX")]
	public class Goldorak : TablePatcher
	{
		[NameMatch("LFLogo", Ref = "Playfield/Flippers/LeftFlipper")]
		[NameMatch("RFLogo", Ref = "Playfield/Flippers/RightFlipper")]
		public void ReparentFlippers(PrimitiveComponent flipper, GameObject gameObject, ref GameObject parent)
		{
			PatcherUtil.Reparent(gameObject, parent);

			flipper.Position = Vector2.zero;

			// rotation is set in the original data, reparenting caused the flippers to be rotated wrong => fixing the rotation
			// flipper.Rotation.y = 0;

		}
	}
}
