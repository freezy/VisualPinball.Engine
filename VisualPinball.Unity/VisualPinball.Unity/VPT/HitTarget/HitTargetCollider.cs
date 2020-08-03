using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Unity.Physics.Collider;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.Physics.Event;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.VPT.HitTarget
{
	public static class HitTargetCollider
	{
		public static void Collide(ref BallData ball, ref NativeQueue<EventData>.ParallelWriter hitEvents,
			ref HitTargetAnimationData animationData, in float3 normal, in CollisionEventData collEvent,
			in Collider coll, ref Random random)
		{
			var dot = -math.dot(collEvent.HitNormal, ball.Velocity);
			BallCollider.Collide3DWall(ref ball, in coll.Header.Material, in collEvent, in normal, ref random);

			if (coll.FireEvents && dot >= coll.Threshold && !animationData.IsDropped) {
				animationData.HitEvent = true;
				//todo m_obj->m_currentHitThreshold = dot;
				Collider.FireHitEvent(ref ball, ref hitEvents, in coll.Header);
			}
		}
	}
}
