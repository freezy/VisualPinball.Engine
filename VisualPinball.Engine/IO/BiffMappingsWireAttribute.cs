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
using VisualPinball.Engine.VPT.Mappings;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.IO
{
	public class BiffMappingsWireAttribute : BiffAttribute
	{
		public BiffMappingsWireAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			ParseValue(obj, reader, len, ReadMappingsWire);
		}

		public override void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter)
		{
			WriteValue<TItem, MappingsWireData>(obj, writer, (w, v) => WriteMappingsWire(w, v, hashWriter), hashWriter, x => 0);
		}

		private static void WriteMappingsWire(BinaryWriter writer, BiffData value, HashWriter hashWriter)
		{
			value.Write(writer, hashWriter);
		}

		private static MappingsWireData ReadMappingsWire(BinaryReader reader, int len)
		{
			return new MappingsWireData(reader);
		}
	}
}
