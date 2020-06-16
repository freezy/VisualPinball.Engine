using Unity.Entities;
using Unity.Profiling;
using VisualPinball.Unity.Physics.SystemGroup;

namespace VisualPinball.Unity.VPT.Plunger
{
	[UpdateInGroup(typeof(UpdateAnimationsSystemGroup))]
	public class PlungerAnimationSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("PlungerAnimationSystem");

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			var animationDatas = GetComponentDataFromEntity<PlungerAnimationData>();

			Entities
				.WithNativeDisableParallelForRestriction(animationDatas)
				.ForEach((in PlungerMovementData movementData, in PlungerStaticData staticData) =>
			{
				marker.Begin();

				var frame0 = (int)((movementData.Position - staticData.FrameStart) / (staticData.FrameEnd - staticData.FrameStart) * (staticData.NumFrames - 1) + 0.5f);
				var frame = frame0 < 0 ? 0 : frame0 >= staticData.NumFrames ? staticData.NumFrames - 1 : frame0;


				if (animationDatas.Exists(staticData.RodEntity)) {
					var rodAnimData = animationDatas[staticData.RodEntity];
					if (rodAnimData.CurrentFrame != frame) {
						rodAnimData.CurrentFrame = frame;
						rodAnimData.IsDirty = true;
						animationDatas[staticData.RodEntity] = rodAnimData;
					}
				}

				if (animationDatas.Exists(staticData.SpringEntity)) {
					var springAnimData = animationDatas[staticData.SpringEntity];
					if (springAnimData.CurrentFrame != frame) {
						springAnimData.CurrentFrame = frame;
						springAnimData.IsDirty = true;
						animationDatas[staticData.SpringEntity] = springAnimData;
					}
				}

				if (animationDatas.Exists(staticData.FlatEntity)) {
					var flatAnimData = animationDatas[staticData.FlatEntity];
					if (flatAnimData.CurrentFrame != frame) {
						flatAnimData.CurrentFrame = frame;
						flatAnimData.IsDirty = true;
						animationDatas[staticData.FlatEntity] = flatAnimData;
					}
				}

				marker.End();

			}).Run();
		}
	}
}
