using Unity.Collections;
using Unity.Entities;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	public static class TriggerCollider
	{
		/// <summary>
		/// Collides without triggering the animation, which is what the
		/// <see cref="Poly3DCollider"/> does.
		/// </summary>
		public static void Collide(ref BallData ball, ref NativeQueue<EventData>.ParallelWriter events,
			ref CollisionEventData collEvent, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, in Collider coll)
		{
			var _ = default(TriggerAnimationData);
			Collide(ref ball, ref events, ref collEvent, ref insideOfs, ref _, in coll, false);
		}

		public static void Collide(ref BallData ball, ref NativeQueue<EventData>.ParallelWriter events,
			ref CollisionEventData collEvent, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs,
			ref TriggerAnimationData animationData, in Collider coll)
		{
			Collide(ref ball, ref events, ref collEvent, ref insideOfs, ref animationData, in coll, true);
		}

		private static void Collide(ref BallData ball, ref NativeQueue<EventData>.ParallelWriter events,
			ref CollisionEventData collEvent, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs,
			ref TriggerAnimationData animationData, in Collider coll, bool animate)
		{
			// todo?
			// if (!ball.isRealBall()) {
			// 	return;
			// }

			var insideOf = BallData.IsInsideOf(in insideOfs, coll.Entity);
			if (collEvent.HitFlag == insideOf) {                                         // Hit == NotAlreadyHit
				ball.Position += PhysicsConstants.StaticTime * ball.Velocity;            // move ball slightly forward

				if (!insideOf) {
					BallData.SetInsideOf(ref insideOfs, coll.Entity);
					if (animate) {
						animationData.HitEvent = true;
					}

					events.Enqueue(new EventData(EventId.HitEventsHit, coll.Entity, true));

				} else {
					BallData.SetOutsideOf(ref insideOfs, coll.Entity);
					if (animate) {
						animationData.UnHitEvent = true;
					}

					events.Enqueue(new EventData(EventId.HitEventsUnhit, coll.Entity, true));
				}
			}
		}
	}
}
