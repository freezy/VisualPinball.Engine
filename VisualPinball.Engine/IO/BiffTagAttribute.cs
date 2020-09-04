// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

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
