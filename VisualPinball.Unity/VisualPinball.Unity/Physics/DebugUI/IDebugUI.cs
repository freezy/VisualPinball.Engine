using Unity.Entities;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Physics.DebugUI
{
	public interface IDebugUINew : IEngine
	{
		void Init(TableBehavior tableBehavior);

		void OnPhysicsUpdate(int numSteps, float elapsedTotalMilliseconds);

		void OnRegisterFlipper(Entity entity, string name);

		void OnCreateBall(Entity entity);
	}
}
