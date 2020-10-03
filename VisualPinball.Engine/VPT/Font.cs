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

using System;
using System.IO;
using System.Text;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT
{
	[Serializable]
	public class Font
	{
		public string Name;
		public ushort Weight;
		public uint Size;
		public bool Italic;

		public Font(BinaryReader reader)
		{
			reader.BaseStream.Seek(3, SeekOrigin.Current);
			Italic = reader.ReadByte() > 0;
			Weight = reader.ReadUInt16();
			Size = reader.ReadUInt32();
			var nameLen = (int)reader.ReadByte();
			Name = Encoding.Default.GetString(reader.ReadBytes(nameLen));
		}

		public void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write(new byte[]{ 0x01, 0x0, 0x0 });
			writer.Write((byte)(Italic ? 0x02 : 0x0));
			writer.Write(Weight);
			writer.Write(Size);
			writer.Write((byte)Name.Length);
			writer.Write(Encoding.Default.GetBytes(Name));
		}
	}
}
