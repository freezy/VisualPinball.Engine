using VisualPinball.Engine.VPT.Sound;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Scriptable object wrapper for plain VPX sound data. This will allow us to operate on sound data one a time
	/// for things like undo tracking, rather than needing to serialize the whole table (sidecar) and everything on it
	/// </summary>
	public class TableSerializedSound : TableSerializedData<SoundData>
	{
		public static TableSerializedSound Create(SoundData data) => GenericCreate<TableSerializedSound>(data);
	}
}
