using MemoryPack;

namespace VisualPinball.Unity.Editor.Packaging
{
	public class MemoryPackDataPacker : IDataPacker
	{
		public T Unpack<T>(byte[] data) => MemoryPackSerializer.Deserialize<T>(data);

		public byte[] Pack() => MemoryPackSerializer.Serialize(this);
	}
}
