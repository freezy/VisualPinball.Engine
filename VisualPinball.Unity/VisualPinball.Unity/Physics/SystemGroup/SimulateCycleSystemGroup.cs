using System.Collections.Generic;
using NLog;
using Unity.Entities;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Physics.Collision;

namespace VisualPinball.Unity.Physics.SystemGroup
{
	[DisableAutoCreation]
	public class SimulateCycleSystemGroup : ComponentSystemGroup
	{
		public double DTime;

		public override IEnumerable<ComponentSystemBase> Systems => _systemsToUpdate;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly List<ComponentSystemBase> _systemsToUpdate = new List<ComponentSystemBase>();
		private BallBroadPhaseSystem _ballBroadPhaseSystem;
		private UpdateDisplacementSystemGroup _displacementSystemGroup;

		protected override void OnCreate()
		{
			_ballBroadPhaseSystem = World.GetOrCreateSystem<BallBroadPhaseSystem>();
			_displacementSystemGroup = World.GetOrCreateSystem<UpdateDisplacementSystemGroup>();
			_systemsToUpdate.Add(_ballBroadPhaseSystem);
			_systemsToUpdate.Add(_displacementSystemGroup);
		}

		protected override void OnUpdate()
		{
			var sim = World.GetExistingSystem<VisualPinballSimulationSystemGroup>();

			DTime = sim.PhysicsDiffTime;
			while (DTime > 0) {

				//Logger.Info("     ({0}) Player::PhysicsSimulateCycle (loop)\n", DTime);

				var hitTime = DTime;

				_ballBroadPhaseSystem.Update();
				_displacementSystemGroup.Update();

				DTime -= hitTime;
			}
		}
	}
}
