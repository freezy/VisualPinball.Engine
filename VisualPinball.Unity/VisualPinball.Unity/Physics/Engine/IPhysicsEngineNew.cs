using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity.Physics.Engine
{
	public interface IPhysicsEngineNew : IEngine
	{
		void OnRegisterFlipper(Entity entity, string name);
		void OnPhysicsUpdate(int numSteps, float processingTime);
		void OnCreateBall(Entity entity, float3 position, float3 velocity, float radius, float mass);
		void OnRotateToEnd(Entity entity);
		void OnRotateToStart(Entity entity);
		bool UsePureEntity { get; }
	}
}
