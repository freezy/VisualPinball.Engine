using System;
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

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			if (Type == typeof(string)) {
				SetValue(obj, ReadString(reader, len));

			} else if (Type == typeof(string[])) {
				var arr = GetValue(obj) as string[];
				if (Count > 1) {
					for (var i = 0; i < Count; i++) {
						arr[i] = ReadString(reader, len);
					}
				} else {
					arr[Index] = ReadString(reader, len);
				}
			}
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
