using System.IO;
using System.Linq;
using System.Text;

namespace VisualPinball.Engine.IO
{
	public class BiffStringAttribute : BiffAttribute
	{
		/// <summary>
		/// Wide strings have a zero byte between each character.
		/// </summary>
		public bool IsWideString;

		/// <summary>
		/// If true, parse length from preceding int32 (like wide strings,
		/// but don't interpret string as wide string).
		/// </summary>
		public bool HasExplicitLength;

		public BiffStringAttribute(string name) : base(name) { }

		public override void Parse<TItem>(TItem obj, BinaryReader reader, int len)
		{
			ParseValue(obj, reader, len, ReadString);
		}

		public override void Write<TItem>(TItem obj, BinaryWriter writer)
		{
			WriteValue<TItem, string>(obj, writer, WriteString, len => IsStreaming ? 0 : len + 4);
		}

		private string ReadString(BinaryReader reader, int len)
		{
			byte[] bytes;
			if (IsWideString) {
				var wideLen = reader.ReadInt32();
				bytes = reader.ReadBytes(wideLen).Where((x, i) => i % 2 == 0).ToArray();
			} else {
				if (HasExplicitLength) {
					var explicitLength = reader.ReadInt32();
					bytes = reader.ReadBytes(explicitLength).ToArray();
				} else {
					bytes = IsStreaming ? reader.ReadBytes(len) : reader.ReadBytes(len).Skip(4).ToArray();
				}
			}
			return Encoding.ASCII.GetString(bytes);
		}

		private void WriteString(BinaryWriter writer, string value)
		{
			var bytes = Encoding.ASCII.GetBytes(value);
			if (IsWideString) {
				bytes = bytes.SelectMany(b => new byte[] {b, 0x0}).ToArray();
			}
			writer.Write(bytes.Length);
			writer.Write(bytes);
		}
	}
}
