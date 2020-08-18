using Unity.Entities;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	public interface IDebugUI : IEngine
	{
		/// <summary>
		/// Initializes the debug UI. This is called in the table's Start() method.
		/// </summary>
		/// <param name="tableAuthoring">Table component</param>
		void Init(TableAuthoring tableAuthoring);

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

		/// <summary>
		/// Add new property to debug window.
		/// If type T is not recognized it is treated as beginning of new group.
		/// </summary>
		/// <typeparam name="T">Data type, like Vector3, float, int, ...</typeparam>
		/// <param name="parentIdx">index to parent. If =-1 it means root.</param>
		/// <param name="name">Label or name of group.</param>
		/// <param name="value">Initial value.</param>
		/// <param name="tip">Message to display as tooltip.</param>
		/// <returns>Index of property. Use it as parentIdx or propIdx.</returns>
		int AddProperty<T>(int parentIdx, string name, T value, string tip = null);

		/// <summary>
		/// Get property value from DebugUI.
		/// </summary>
		/// <typeparam name="T">Data type, like Vector3, float, int, ...</typeparam>
		/// <param name="propIdx"></param>
		/// <param name="value">Output where new value will be writen.</param>
		/// <returns>true if value is changed.</returns>
		bool GetProperty<T>(int propIdx, ref T value);


		/// <summary>
		/// Set property value in  DebugUI.
		/// </summary>
		/// <typeparam name="T">Data type, like Vector3, float, int, ...</typeparam>
		/// <param name="propIdx">Index of property.</param>
		/// <param name="value">New value for property.</param>
		void SetProperty<T>(int propIdx, T value);

		/// <summary>
		/// One line to add property (to Quick group) and sync value.
		/// Property is recognized base on name & type. So, you can use same name for different types
		/// </summary>
		/// <typeparam name="T">Data type, like Vector3, float, int, ...</typeparam>
		/// <param name="name">Label for property.</param>
		/// <param name="value">Current value as input. Can be updated.</param>
		/// <param name="tip">Message to display as tooltip.</param>
		/// <returns>true if value is changed.</returns>
		bool QuickPropertySync<T>(string name, ref T value, string tip = null);
	}
}
