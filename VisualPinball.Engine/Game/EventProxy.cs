using System;
using System.Collections.Generic;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Game
{
	public class EventProxy
	{
		/// <summary>
		/// While playing and the ball hits the mesh the hit threshold is updated here
		/// </summary>
		public float CurrentHitThreshold = 0;

		public bool SingleEvents = true;
		public readonly EventProxy[] EventCollection = new EventProxy[0];
		public readonly List<int> EventCollectionItemPos = new List<int>();

		private readonly IPlayable Playable;

		/// <summary>
		/// Logic executed on collision.
		///
		/// This replaces the dreaded object casts in VP where the hit logic must
		/// aware of the underlying object.
		/// </summary>
		public Action<HitObject, Ball, float> OnCollision;

		/// <summary>
		/// If implemented and false is returned, the hit test is skipped.
		/// </summary>
		public Func<bool> AbortHitTest;

		public EventProxy(IPlayable playable) {
			Playable = playable;
		}

		public void FireVoidEvent(Event e)
		{
			FireDispID(e);
		}

		public void FireVoidEventParam(Event e, params dynamic[] param)
		{
			FireDispID(e, param);
			//logger().Info("[%s] fireGroupEvent(%s, %s)", this.Playable.GetName(), e, data);
		}

		public void FireGroupEvent(Event e)
		{
			for (var i = 0; i < EventCollection.Length; i++) {
				EventCollection[i].FireVoidEventParam(e, EventCollectionItemPos[i]);
			}

			if (SingleEvents) {
				FireDispID(e);
			}

			//logger().Info("[%s] fireGroupEvent(%s)", this.Playable.GetName(), e);
		}

		private void FireDispID(Event e, params dynamic[] param)
		{
			// TODO API
			// if (isScriptable(Playable)) {
			// 	Playable.GetApi().Emit(getEventName(e), param);
			// 	//logger().Info("[%s] fireDispID(%s)", this.Playable.GetName(), e);
			// }
		}
	}
}
