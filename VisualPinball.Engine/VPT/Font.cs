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
