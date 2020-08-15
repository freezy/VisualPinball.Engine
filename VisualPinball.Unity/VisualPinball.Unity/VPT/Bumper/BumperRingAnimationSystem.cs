using Unity.Entities;
using Unity.Profiling;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(UpdateAnimationsSystemGroup))]
	public class BumperRingAnimationSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("BumperRingAnimationSystem");

		protected override void OnUpdate()
		{
			var dTime = Time.DeltaTime * 1000;
			var marker = PerfMarker;

			Entities
				.WithName("BumperRingAnimationJob")
				.ForEach((ref BumperRingAnimationData data) => {

					// todo visibility - skip if invisible

					marker.Begin();

					var limit = data.DropOffset + data.HeightScale * 0.5f * data.ScaleZ;
					if (data.IsHit) {
						data.DoAnimate = true;
						data.AnimateDown = true;
						data.IsHit = false;
					}
					if (data.DoAnimate) {
						var step = data.Speed * data.ScaleZ;
						if (data.AnimateDown) {
							step = -step;
						}
						data.Offset += step * dTime;
						if (data.AnimateDown) {
							if (data.Offset <= -limit) {
								data.Offset = -limit;
								data.AnimateDown = false;
							}
						} else {
							if (data.Offset >= 0.0f) {
								data.Offset = 0.0f;
								data.DoAnimate = false;
							}
						}
					}

					marker.End();

				}).Run();
		}
	}
}
