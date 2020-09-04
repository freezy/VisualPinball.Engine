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
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

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
			ParseValue(obj, reader, len, ReadColor);
		}

		public override void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter)
		{
			WriteValue<TItem, Color>(obj, writer, WriteColor, hashWriter);
		}

		private static void WriteColor(BinaryWriter writer, Color value)
		{
			writer.Write(value.Bgr);
		}

		private Color ReadColor(BinaryReader reader, int len)
		{
			return new Color(reader.ReadInt32(), ColorFormat);
		}
	}
}
