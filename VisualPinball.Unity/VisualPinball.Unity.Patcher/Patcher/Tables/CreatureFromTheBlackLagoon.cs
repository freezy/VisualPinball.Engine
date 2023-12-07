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
	[MetaMatch(TableName = "Creature From The Black Lagoon", AuthorName = "fuzzel, flupper1, rothbauerw")]
	public class CreatureFromTheBlackLagoon : TablePatcher
	{
		[NameMatch("batleft", Ref = "Playfield/Flippers/LeftFlipper")]
		[NameMatch("batright", Ref = "Playfield/Flippers/RightFlipper")]
		public void ReparentFlippers(PrimitiveComponent flipper, GameObject gameObject, ref GameObject parent)
		{
			PatcherUtil.Reparent(gameObject, parent);

			flipper.Position = Vector2.zero;
			// flipper.ObjectRotation.z = 0;
		}

		[NameMatch("batleftshadow")]
		[NameMatch("batrightshadow")]
		public void RemoveFlipperShadow(GameObject gameObject)
		{
			gameObject.SetActive(false);
		}

		[NameMatch("sw55")]
		public void FixSw55(KickerComponent kickerComponent)
		{
			kickerComponent.Coils[0].Speed = 20;
			kickerComponent.Coils[0].Angle = 60;
		}

		[NameMatch("sw56")]
		public void FixSw56(KickerComponent kickerComponent)
		{
			kickerComponent.Coils[0].Speed = 12;
			kickerComponent.Coils[0].Angle = 60;
		}
	}
}
