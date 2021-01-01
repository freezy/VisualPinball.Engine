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

namespace VisualPinball.Unity.Patcher
{
	[MetaMatch(TableName = "Mississipi", AuthorName = "jpsalas, akiles50000, Loserman")]
	public class Mississippi
	{
		[NameMatch("lrail1", Ref = "Ramps/lrail1/LeftWall")] // left outside wall to the left
		[NameMatch("rrail1", Ref = "Ramps/rrail1/LeftWall")] // left inside wall to the right
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
		[NameMatch("wall13", Ref = "Surfaces/wall13/Top")]
		[NameMatch("wall14", Ref = "Surfaces/wall14/Top")]
		[NameMatch("wall15", Ref = "Surfaces/wall15/Top")]
		[NameMatch("wall18", Ref = "Surfaces/wall18/Top")]
		[NameMatch("wall19", Ref = "Surfaces/wall19/Top")]
		[NameMatch("wall22", Ref = "Surfaces/wall22/Top")]
		[NameMatch("wall23", Ref = "Surfaces/wall23/Top")]
		[NameMatch("wall359", Ref = "Surfaces/wall359/Top")]
		[NameMatch("apron", Ref = "Surfaces/apron/Top")]
		public void SetTransparentDepthPrepassEnabled(GameObject gameObject, ref GameObject child)
		{
			if (gameObject == child)
			{
				RenderPipeline.Current.MaterialAdapter.SetTransparentDepthPrepassEnabled(gameObject);
			}
		}
	}
}
