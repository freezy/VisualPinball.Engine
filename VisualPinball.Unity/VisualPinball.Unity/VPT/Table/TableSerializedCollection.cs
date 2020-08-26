using VisualPinball.Engine.VPT.Collection;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Scriptable object wrapper for plain VPX collection data.
	/// </summary>
	public class TableSerializedCollection : TableSerializedData<CollectionData>
	{
		public static TableSerializedCollection Create(CollectionData data) => GenericCreate<TableSerializedCollection>(data);
	}
}
