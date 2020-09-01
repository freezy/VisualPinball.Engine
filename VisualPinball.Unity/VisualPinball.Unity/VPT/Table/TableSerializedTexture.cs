using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Scriptable object wrapper for plain VPX texture data. This will allow us to operate on texture data one a time
	/// for things like undo tracking, rather than needing to serialize the whole table (sidecar) and everything on it
	/// </summary>
	public class TableSerializedTexture : TableSerializedData<TextureData>
	{
		public static TableSerializedTexture Create(TextureData data) => GenericCreate<TableSerializedTexture>(data);
	}
}
