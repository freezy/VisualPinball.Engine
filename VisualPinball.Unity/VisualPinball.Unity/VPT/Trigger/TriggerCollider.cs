using Unity.Entities;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Physics.Collider;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.VPT.Trigger
{
	public static class TriggerCollider
	{
		public static void Collide(ref BallData ball, ref CollisionEventData collEvent,
			ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, ref TriggerAnimationData animationData,
			in Collider coll)
		{
			// todo?
			// if (!ball.isRealBall()) {
			// 	return;
			// }

			var insideOf = BallData.IsInsideOf(in insideOfs, coll.Entity);
			if (collEvent.HitFlag == insideOf) {                                            // Hit == NotAlreadyHit
				ball.Position += PhysicsConstants.StaticTime * ball.Velocity; // move ball slightly forward

				if (!insideOf) {
					BallData.SetInsideOf(ref insideOfs, coll.Entity);
					animationData.HitEvent = true;

					// todo event
					//this.obj!.fireGroupEvent(Event.HitEventsHit);

				} else {
					BallData.SetOutsideOf(ref insideOfs, coll.Entity);
					animationData.UnHitEvent = true;

					// todo event
					//this.obj!.fireGroupEvent(Event.HitEventsUnhit);
				}
			}
			else {

			}
		}
	}
}
