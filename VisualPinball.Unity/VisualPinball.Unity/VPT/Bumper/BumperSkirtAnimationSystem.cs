using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(UpdateAnimationsSystemGroup))]
	public class BumperSkirtAnimationSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("BumperSkirtAnimationSystem");

		protected override void OnUpdate()
		{
			var dTime = Time.DeltaTime * 1000;
			var marker = PerfMarker;

			Entities
				.WithName("BumperSkirtAnimationJob")
				.ForEach((ref BumperSkirtAnimationData data) => {

					// todo visibility - skip if invisible

					marker.Begin();

					var isHit = data.HitEvent;
					data.HitEvent = false;

					if (data.EnableAnimation) {
						if (isHit) {
							data.DoAnimate = true;
							UpdateSkirt(ref data);
							data.AnimationCounter = 0.0f;
						}
						if (data.DoAnimate) {
							data.AnimationCounter += dTime;
							if (data.AnimationCounter > 160.0f) {
								data.DoAnimate = false;
								ResetSkirt(ref data);
							}
						}
					} else if (data.DoUpdate) { // do a single update if the animation was turned off via script
						data.DoUpdate = false;
						ResetSkirt(ref data);
					}

					marker.End();

				}).Run();
		}

		private static void UpdateSkirt(ref BumperSkirtAnimationData data)
		{
			const float skirtTilt = 5.0f;

			var hitX = data.BallPosition.x;
			var hitY = data.BallPosition.y;
			var dy = math.abs(hitY - data.Center.y);
			if (dy == 0.0f) {
				dy = 0.000001f;
			}

			var dx = math.abs(hitX - data.Center.x);
			var skirtA = math.atan(dx / dy);
			data.Rotation.x = math.cos(skirtA) * skirtTilt;
			data.Rotation.y = math.sin(skirtA) * skirtTilt;
			if (data.Center.y < hitY) {
				data.Rotation.x = -data.Rotation.x;
			}

			if (data.Center.x > hitX) {
				data.Rotation.y = -data.Rotation.y;
			}
		}

		private static void ResetSkirt(ref BumperSkirtAnimationData data)
		{
			data.Rotation = new float2(0, 0);
		}
	}
}
