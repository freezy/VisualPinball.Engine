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
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using UnityEngine;
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity.Patcher
{
	[MetaMatch(TableName = "Wipe Out Premier 1993", AuthorName = "Edizzle & Kiwi")]
	public class WipeOut
	{
		[NameMatch("Prim_RightFlipper", Ref="Playfield/Flippers/RightFlipper")]
		[NameMatch("Prim_LeftFlipper", Ref="Playfield/Flippers/LeftFlipper")]
		public void ReparentFlippers(PrimitiveComponent primitive, GameObject gameObject, ref GameObject parent)
		{
			PatcherUtil.Reparent(gameObject, parent);
			primitive.Position = Vector3.zero;
		}
	}
}
