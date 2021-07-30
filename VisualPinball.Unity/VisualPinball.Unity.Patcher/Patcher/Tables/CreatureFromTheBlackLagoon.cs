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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Kicker;

namespace VisualPinball.Unity.Patcher
{
	[MetaMatch(TableName = "Creature From The Black Lagoon", AuthorName = "fuzzel, flupper1, rothbauerw")]
	public class CreatureFromTheBlackLagoon
	{
		[NameMatch("batleft", Ref = "Flippers/LeftFlipper")]
		[NameMatch("batright", Ref = "Flippers/RightFlipper")]
		public void ReparentFlippers(Primitive flipper, GameObject gameObject, ref GameObject parent)
		{
			PatcherUtil.Reparent(gameObject, parent);
			gameObject.transform.localPosition = new Vector3(0, 0, 0);
	
			flipper.Data.Position.X = 0;
			flipper.Data.Position.Y = 0;

			flipper.Data.RotAndTra[8] = 0;
		}

		[NameMatch("batleftshadow")]
		[NameMatch("batrightshadow")]
		public void RemoveFlipperShadow(Primitive primitive)
		{
			primitive.Data.IsVisible = false;
		}

		[NameMatch("sw55")]
		public void FixSw55(Kicker kicker)
		{
			kicker.Data.Angle = 60;
			kicker.Data.Speed = 20;
		}

		[NameMatch("sw56")]
		public void FixSw56(Kicker kicker)
		{
			kicker.Data.Angle = 60;
			kicker.Data.Speed = 12;
		}
	}
}
