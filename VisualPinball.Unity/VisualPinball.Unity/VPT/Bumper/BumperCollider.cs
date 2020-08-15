using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	public static class BumperCollider
	{
		public static void Collide(ref BallData ball, ref NativeQueue<EventData>.ParallelWriter events,
			ref CollisionEventData collEvent, ref BumperRingAnimationData ringData, ref BumperSkirtAnimationData skirtData,
			in Collider collider, in BumperStaticData data, ref Random random)
		{
			// todo
			// if (!m_enabled) return;

			var dot = math.dot(collEvent.HitNormal, ball.Velocity); // needs to be computed before Collide3DWall()!
			var material = collider.Material;
			BallCollider.Collide3DWall(ref ball, in material, in collEvent, in collEvent.HitNormal, ref random); // reflect ball from wall

			if (data.HitEvent && dot <= -data.Threshold) { // if velocity greater than threshold level

				ball.Velocity += collEvent.HitNormal * data.Force; // add a chunk of velocity to drive ball away

				ringData.IsHit = true;
				skirtData.HitEvent = true;
				skirtData.BallPosition = ball.Position;

				events.Enqueue(new EventData(EventId.HitEventsHit, collider.Entity, true));
			}
		}
	}
}
