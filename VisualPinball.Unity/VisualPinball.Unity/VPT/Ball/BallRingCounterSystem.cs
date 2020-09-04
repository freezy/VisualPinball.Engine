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

using Unity.Entities;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	[DisableAutoCreation]
	internal class BallRingCounterSystem : SystemBase
	{
		public const int MaxBallTrailPos = 10;

		protected override void OnUpdate()
		{
			var lastPositionBuffer = GetBufferFromEntity<BallLastPositionsBufferElement>();
			Entities.ForEach((Entity entity, ref BallData ball) => {

				var posIdx = ball.RingCounterOldPos / (10000 / PhysicsConstants.PhysicsStepTime);
				var lastPositions = lastPositionBuffer[entity];
				var lastPosition = lastPositions[posIdx];
				lastPosition.Value = ball.Position;
				lastPositions[posIdx] = lastPosition;

				ball.RingCounterOldPos++;
				if (ball.RingCounterOldPos == MaxBallTrailPos * (10000 / PhysicsConstants.PhysicsStepTime)) {
					ball.RingCounterOldPos = 0;
				}

			}).Run();
		}
	}
}
