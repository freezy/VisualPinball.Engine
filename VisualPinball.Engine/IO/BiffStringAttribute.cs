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

		private string ReadString(BinaryReader reader, int len)
		{
			byte[] bytes;
			if (IsWideString) {
				var wideLen = reader.ReadInt32();
				bytes = reader.ReadBytes(wideLen).Where((x, i) => i % 2 == 0).ToArray();
			} else {
				if (HasExplicitLength) {
					var wideLen = reader.ReadInt32();
					bytes = reader.ReadBytes(wideLen).ToArray();
				} else {
					bytes = IsStreaming ? reader.ReadBytes(len) : reader.ReadBytes(len).Skip(4).ToArray();
				}
			}
			return Encoding.ASCII.GetString(bytes);
		}
	}
}
