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

using UnityEngine;

namespace VisualPinball.Unity.Patcher
{
	[AnyMatch]
	public class Defaults
	{
		[NameMatch("BallShadow1")]
		[NameMatch("BallShadow2")]
		[NameMatch("BallShadow3")]
		[NameMatch("BallShadow4")]
		[NameMatch("BallShadow5")]
		[NameMatch("BallShadow6")]
		[NameMatch("BallShadow7")]
		public void RemoveBallShadow(GameObject go)
		{
			go.SetActive(false);
		}

		[NameMatch("FlipperLSh")]
		[NameMatch("FlipperRSh")]
		public void RemoveFlipperShadow(GameObject go)
		{
			go.SetActive(false);
		}

		[NameMatch("Ruler_mm")]
		[NameMatch("Ruler_inches")]
		[NameMatch("Ruler_inches_and_mm")]
		public void RemoveColliders(PrimitiveColliderComponent collider)
		{
			Object.DestroyImmediate(collider);
		}
	}
}
