using System.Collections.Generic;
using NLog;
using Unity.Entities;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Physics.HitTest;

namespace VisualPinball.Unity.Physics
{
	[DisableAutoCreation]
	public class VisualPinballSimulatePhysicsCycleSystemGroup : ComponentSystemGroup
	{
		public double DTime;

		public override IEnumerable<ComponentSystemBase> Systems => _systemsToUpdate;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly List<ComponentSystemBase> _systemsToUpdate = new List<ComponentSystemBase>();
		private VisualPinballHitTestSystemGroup _hitTestSystemGroup;
		private VisualPinballUpdateDisplacementSystemGroup _displacementSystemGroup;

		protected override void OnCreate()
		{
			_hitTestSystemGroup = World.GetOrCreateSystem<VisualPinballHitTestSystemGroup>();
			_displacementSystemGroup = World.GetOrCreateSystem<VisualPinballUpdateDisplacementSystemGroup>();
			_systemsToUpdate.Add(_displacementSystemGroup);
		}

		protected override void OnUpdate()
		{
			var sim = World.GetExistingSystem<VisualPinballSimulationSystemGroup>();

			DTime = sim.PhysicsDiffTime;
			while (DTime > 0) {

				//Logger.Info("     ({0}) Player::PhysicsSimulateCycle (loop)\n", DTime);

				var hitTime = DTime;

				_hitTestSystemGroup.Update();
				_displacementSystemGroup.Update();

				DTime -= hitTime;
			}
		}
	}
}
