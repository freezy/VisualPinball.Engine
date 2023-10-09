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

using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	internal static class BallRingCounterPhysics
	{
		internal static void Update(ref BallData ball)
		{
			var idx = ball.RingCounterOldPos / (10000 / PhysicsConstants.PhysicsStepTime);
			ball.LastPositions[idx] = ball.Position;
			ball.RingCounterOldPos++;
			if (ball.RingCounterOldPos == BallPositions.Count * (10000 / PhysicsConstants.PhysicsStepTime)) {
				ball.RingCounterOldPos = 0;
			}
		}
	}
}
