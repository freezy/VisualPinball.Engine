// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

namespace VisualPinball.Unity
{
	public class VisualPinballScript : MonoBehaviour
	{
		public virtual void OnAwake(TableApi table)
		{
			table.Plunger("Plunger").Init += (sender, args) => {
				KickNewBallToPlunger(table);
			};

			table.Kicker("Drain").Hit += (sender, args) => {
				((KickerApi)sender).DestroyBall();
				KickNewBallToPlunger(table);
			};
		}

		public void KickNewBallToPlunger(TableApi table)
		{
			table.Kicker("BallRelease").CreateBall();
			table.Kicker("BallRelease").Kick(90, 7);
		}
	}
}
