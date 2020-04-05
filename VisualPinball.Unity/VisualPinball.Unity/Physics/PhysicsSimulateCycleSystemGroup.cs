using Unity.Entities;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.Physics
{
	[DisableAutoCreation]
	[UpdateInGroup(typeof(VisualPinballSimulationSystemGroup))]
	public class PhysicsSimulateCycleSystemGroup : ComponentSystemGroup
	{
		public double DTime;

		protected override void OnUpdate()
		{
			var sim = World.DefaultGameObjectInjectionWorld.GetExistingSystem<VisualPinballSimulationSystemGroup>();

			DTime = sim.PhysicsDiffTime;
			while (DTime > 0) {
				var hitTime = DTime;

				World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<UpdateDisplacementSystemGroup>().Update();

				DTime -= hitTime;
			}
		}
	}
}
