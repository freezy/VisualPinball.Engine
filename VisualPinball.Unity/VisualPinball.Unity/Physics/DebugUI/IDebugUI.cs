using Unity.Entities;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Physics.DebugUI
{
	public interface IDebugUI : IEngine
	{
		/// <summary>
		/// Initializes the debug UI. This is called in the table's Start() method.
		/// </summary>
		/// <param name="tableBehavior">Table component</param>
		void Init(TableBehavior tableBehavior);

		/// <summary>
		/// Called when a physics cycle has completed
		/// </summary>
		/// <param name="numSteps">Number of completed ticks</param>
		/// <param name="elapsedTotalMilliseconds">Number of milliseconds since the game started</param>
		void OnPhysicsUpdate(int numSteps, float elapsedTotalMilliseconds);

		/// <summary>
		/// Called when a flipper has been converted to an entity.
		/// </summary>
		/// <param name="entity">Flipper entity</param>
		/// <param name="name">Name of the flipper</param>
		void OnRegisterFlipper(Entity entity, string name);

		/// <summary>
		/// Called when a new ball has been created.
		/// </summary>
		/// <param name="entity">Ball entity</param>
		void OnCreateBall(Entity entity);
	}
}
