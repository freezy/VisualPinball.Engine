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
using VisualPinball.Unity.VisualPinball.Unity.Patcher.Matcher;

namespace VisualPinball.Unity.Patcher
{
	[MetaMatch(TableName = "Mississipi", AuthorName = "jpsalas, akiles50000, Loserman")]
	public class Mississippi : TablePatcher
	{
		[NameMatch("lrail1", Ref = "Playfield/Ramps/lrail1/LeftWall")] // left outside wall to the left
		[NameMatch("rrail1", Ref = "Playfield/Ramps/rrail1/LeftWall")] // left inside wall to the right
		public void SetDoubleSided(GameObject gameObject, ref GameObject child)
		{
			if (gameObject == child)
				RenderPipeline.Current.MaterialAdapter.SetDoubleSided(gameObject);
		}

		/// <summary>
		/// Activate depth prepass or else the image visibility changes depending on distance
		/// </summary>
		/// <param name="gameObject"></param>
		/// <param name="child"></param>
		[NameMatch("wall13", Ref = "Playfield/Walls/wall13/Top")]
		[NameMatch("wall14", Ref = "Playfield/Walls/wall14/Top")]
		[NameMatch("wall15", Ref = "Playfield/Walls/wall15/Top")]
		[NameMatch("wall18", Ref = "Playfield/Walls/wall18/Top")]
		[NameMatch("wall19", Ref = "Playfield/Walls/wall19/Top")]
		[NameMatch("wall22", Ref = "Playfield/Walls/wall22/Top")]
		[NameMatch("wall23", Ref = "Playfield/Walls/wall23/Top")]
		[NameMatch("wall359", Ref = "Playfield/Walls/wall359/Top")]
		[NameMatch("apron", Ref = "Playfield/Walls/apron/Top")]
		public void SetTransparentDepthPrepassEnabled(GameObject gameObject, ref GameObject child)
		{
			if (gameObject == child)
			{
				RenderPipeline.Current.MaterialAdapter.SetTransparentDepthPrepassEnabled(gameObject);
			}
		}
	}
}
