using UnityEngine;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.VPT.Table
{
	/// <summary>
	/// ScriptableObject generic wrapper for any data which needs to be managed indepenently, e.g. for Undo tracking.
	/// These objects types can be handled by <see cref="TableSerializedContainer{T, TData}"/>
	/// </summary>
	/// <typeparam name="TData">The ItemData based class which will be wrapped into a ScriptableObject</typeparam>
	/// <remarks>
	/// These wrapper are used by <see cref="TableSidecar"/> for Textures, Sounds to avoid undo operations on the whole structure
	/// </remarks>
	public class TableSerializedData<TData> : ScriptableObject where TData : ItemData
	{
		public TData Data;

		public static T GenericCreate<T>(TData data) where T : TableSerializedData<TData>
		{
			var tst = CreateInstance<T>();
			tst.Data = data;
			return tst;
		}
	}
}
