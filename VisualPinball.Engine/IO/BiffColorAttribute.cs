using System.IO;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.IO
{
	public class BiffColorAttribute : BiffAttribute
	{
		/// <summary>
		/// For colors, this defines how the integer is encoded.
		/// </summary>
		public ColorFormat ColorFormat = ColorFormat.Bgr;

		public BiffColorAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			if (Type == typeof(Color)) {
				SetValue(obj, new Color(reader.ReadInt32(), ColorFormat));

			} else if (Type == typeof(Color[])) {
				var arr = GetValue(obj) as Color[];
				if (Count > 1) {
					for (var i = 0; i < Count; i++) {
						arr[i] = new Color(reader.ReadInt32(), ColorFormat);
					}
				} else {
					arr[Index] = new Color(reader.ReadInt32(), ColorFormat);
				}
			}
		}
	}
}
