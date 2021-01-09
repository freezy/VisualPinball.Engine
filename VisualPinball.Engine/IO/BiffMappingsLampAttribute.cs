// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
	public class BiffMappingsLampAttribute : BiffAttribute
	{
		public BiffMappingsLampAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			ParseValue(obj, reader, len, ReadMappingsLamp);
		}

		public override void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter)
		{
			WriteValue<TItem, MappingsLampData>(obj, writer, (w, v) => WriteMappingsLamp(w, v, hashWriter), hashWriter, x => 0);
		}

		private static void WriteMappingsLamp(BinaryWriter writer, BiffData value, HashWriter hashWriter)
		{
			value.Write(writer, hashWriter);
		}

		private static MappingsLampData ReadMappingsLamp(BinaryReader reader, int len)
		{
			return new MappingsLampData(reader);
		}
	}
}
