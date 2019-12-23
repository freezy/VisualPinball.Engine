using System;
using System.IO;

namespace VisualPinball.Engine.IO
{
	public class BiffBoolAttribute : BiffAttribute
	{
		public BiffBoolAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			if (Type == typeof(bool)) {
				SetValue(obj, reader.ReadInt32() > 0);

			} else if (Type == typeof(bool[])) {
				var arr = GetValue(obj) as bool[];
				if (Count > 1) {
					for (var i = 0; i < Count; i++) {
						arr[i] = reader.ReadInt32() > 0;
					}
				} else {
					arr[Index] = reader.ReadInt32() > 0;
				}
			}
		}
	}
}
