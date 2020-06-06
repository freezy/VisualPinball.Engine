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
		/// Called when a physics cycle has completed.
		/// </summary>
		/// <param name="physicClockMilliseconds">Physics simulation clock time in miliseconds</param>
		/// <param name="numSteps">Number of completed ticks</param>
		/// <param name="processingTimeMilliseconds">Number of milliseconds of cpu time used for physics simulation</param>	
		void OnPhysicsUpdate(double physicClockMilliseconds, int numSteps, float processingTimeMilliseconds);

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
