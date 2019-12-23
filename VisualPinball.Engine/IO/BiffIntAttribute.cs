using System;
using System.IO;

namespace VisualPinball.Engine.IO
{
	public class BiffIntAttribute : BiffAttribute
	{
		public BiffIntAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			if (Type == typeof(int)) {
				SetValue(obj, reader.ReadInt32());

			} else if (Type == typeof(int[])) {
				var arr = GetValue(obj) as int[];
				if (Count > 1) {
					for (var i = 0; i < Count; i++) {
						arr[i] = reader.ReadInt32();
					}
				} else {
					arr[Index] = reader.ReadInt32();
				}

			} else if (Type == typeof(uint)) {
				SetValue(obj, reader.ReadUInt32());

			} else if (Type == typeof(uint[])) {
				var arr = GetValue(obj) as uint[];
				if (Count > 1) {
					for (var i = 0; i < Count; i++) {
						arr[i] = reader.ReadUInt32();
					}
				} else {
					arr[Index] = reader.ReadUInt32();
				}
			}
		}
	}
}
