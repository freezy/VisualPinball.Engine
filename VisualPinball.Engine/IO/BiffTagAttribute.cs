using System.IO;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.IO
{
	/// <summary>
	/// BIFF tags don't have a value, they're there or not.
	/// </summary>
	public class BiffTagAttribute : BiffAttribute
	{
		public BiffTagAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			ParseValue(obj, reader, len, ReadTag);
		}

		public override void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter)
		{
			WriteValue<TItem, bool>(obj, writer, WriteTag, hashWriter);
		}

		private static bool ReadTag(BinaryReader reader, int len)
		{
			return true;
		}

		private static void WriteTag(BinaryWriter writer, bool value)
		{
			// tags have no data
		}
	}
}
