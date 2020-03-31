using Unity.Entities;
using Unity.Transforms;

namespace VisualPinball.Unity.Game
{
	[UpdateBefore(typeof(TransformSystemGroup))]
	public class VisualPinballSimulationSystemGroup : ComponentSystemGroup
	{
		private const float FixedDeltaTime = 0.001f;

		private bool _initialized; // doing it in OnCreate() results in an endless loop

		protected override void OnUpdate()
		{
			if (!_initialized) {
				//FixedRateUtils.EnableFixedRateWithCatchUp(World.GetOrCreateSystem<SimulationSystemGroup>(), FixedDeltaTime);
				//FixedRateUtils.EnableFixedRateWithCatchUp(this, FixedDeltaTime);
				_initialized = true;
			}
			base.OnUpdate();
		}
	}
}
