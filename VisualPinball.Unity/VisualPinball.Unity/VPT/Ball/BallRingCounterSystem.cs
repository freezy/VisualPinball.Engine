using Unity.Entities;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	[DisableAutoCreation]
	public class BallRingCounterSystem : SystemBase
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
