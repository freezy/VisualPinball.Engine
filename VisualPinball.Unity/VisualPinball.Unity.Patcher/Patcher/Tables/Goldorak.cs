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

using UnityEngine;
using UnityEngine.Rendering;
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity.Patcher
{
	[MetaMatch(TableName = "Goldorak", AuthorName = "Rom (Future PinBall) Javier VPX")]
	public class Goldorak
	{
		[NameMatch("LFLogo", Ref = "Flippers/LeftFlipper")]
		[NameMatch("RFLogo", Ref = "Flippers/RightFlipper")]
		public void ReparentFlippers(GameObject gameObject, ref GameObject parent)
		{
			PatcherUtil.Reparent(gameObject, parent);
		}
	}
}
