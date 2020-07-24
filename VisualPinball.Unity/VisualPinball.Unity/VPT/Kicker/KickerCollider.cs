using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.Physics.Event;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.VPT.Kicker
{
	public static class KickerCollider
	{

		public static void Collide(ref BallData ball, ref NativeQueue<EventData>.ParallelWriter hitEvents,
			in CollisionEventData collEvent, in float3[] hitMesh)
		{

		}
	}
}
