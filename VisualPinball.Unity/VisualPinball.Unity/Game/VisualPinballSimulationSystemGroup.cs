using Unity.Entities;
using Unity.Transforms;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Physics;

namespace VisualPinball.Unity.Game
{
	/// <summary>
	/// Main physics simulation system, executed once per frame.
	/// </summary>
	[UpdateBefore(typeof(TransformSystemGroup))]
	public class VisualPinballSimulationSystemGroup : ComponentSystemGroup
	{
		public double PhysicsDiffTime;

		private long _nextPhysicsFrameTime;
		private long _currentPhysicsTime;

		protected override void OnUpdate()
		{
			var initialTimeUsec = (long)(Time.ElapsedTime * 1000000);
			var curPhysicsFrameTime = _currentPhysicsTime == 0
				? (long) (initialTimeUsec - Time.DeltaTime * 1000000)
				: _currentPhysicsTime;

			while (curPhysicsFrameTime < initialTimeUsec) {

				PhysicsDiffTime = (_nextPhysicsFrameTime - curPhysicsFrameTime) * (1.0 / PhysicsConstants.DefaultStepTime);

				// update velocities
				World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<UpdateVelocitiesSystemGroup>().Update();

				// simulate cycle
				World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<PhysicsSimulateCycleSystemGroup>().Update();

				// new cycle, on physics frame boundary
				curPhysicsFrameTime = _nextPhysicsFrameTime;

				// advance physics position
				_nextPhysicsFrameTime += PhysicsConstants.PhysicsStepTime;
			}

			_currentPhysicsTime = curPhysicsFrameTime;
		}

	}
}
