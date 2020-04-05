using System.Collections.Generic;
using NLog;
using Unity.Entities;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.Physics
{
	[DisableAutoCreation]
	public class VisualPinballSimulatePhysicsCycleSystemGroup : ComponentSystemGroup
	{
		public double DTime;

		public override IEnumerable<ComponentSystemBase> Systems => _systemsToUpdate;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly List<ComponentSystemBase> _systemsToUpdate = new List<ComponentSystemBase>();
		private VisualPinballUpdateDisplacementSystemGroup _displacementSystemGroup;

		protected override void OnCreate()
		{
			_displacementSystemGroup = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<VisualPinballUpdateDisplacementSystemGroup>();
			_systemsToUpdate.Add(_displacementSystemGroup);
		}

		protected override void OnUpdate()
		{
			var sim = World.DefaultGameObjectInjectionWorld.GetExistingSystem<VisualPinballSimulationSystemGroup>();

			DTime = sim.PhysicsDiffTime;
			while (DTime > 0) {

				//Logger.Info("     ({0}) Player::PhysicsSimulateCycle (loop)\n", DTime);

				var hitTime = DTime;

				_displacementSystemGroup.Update();

				DTime -= hitTime;
			}
		}
	}
}
