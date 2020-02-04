using System.IO;
using System.Text;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Sound
{
	public class SoundData : ItemData
	{
		public override string Name { get; set; }
		public string Path;
		public string InternalName;
		public WaveFormat Wfx;
		public byte[] Data;

		public byte OutputTarget = SoundOutTypes.Table;
		public int Volume;
		public int Balance;
		public int Fade;

		public SoundData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(reader);
		}

		public byte[] GetHeader() {
			using (var stream = new MemoryStream())
			using (var writer = new BinaryWriter(stream)) {
				writer.Write(Encoding.ASCII.GetBytes("RIFF"));
				writer.Write(Data.Length + 36); // 9411670
				writer.Write(Encoding.ASCII.GetBytes("WAVE"));
				writer.Write(Encoding.ASCII.GetBytes("fmt "));
				writer.Write(16);
				writer.Write((short)Wfx.FormatTag);
				writer.Write((short)Wfx.Channels);
				writer.Write((int)Wfx.SamplesPerSec);
				writer.Write((int)(Wfx.SamplesPerSec * Wfx.BitsPerSample * Wfx.Channels / 8));
				writer.Write((short)Wfx.BlockAlign);
				writer.Write((short)Wfx.BitsPerSample);
				writer.Write(Encoding.ASCII.GetBytes("data"));
				writer.Write(Data.Length);
				return stream.ToArray();
			}
		}

		private void Load(BinaryReader reader)
		{
			for (var i = 0; i < 10; i++)
			{
				int len;
				switch (i) {
					case 0:
						len = reader.ReadInt32();
						Name = Encoding.ASCII.GetString(reader.ReadBytes(len));
						break;
					case 1:
						len = reader.ReadInt32();
						Path = Encoding.ASCII.GetString(reader.ReadBytes(len));
						break;
					case 2:
						len = reader.ReadInt32();
						InternalName = Encoding.ASCII.GetString(reader.ReadBytes(len));
						break;
					case 3: Wfx = new WaveFormat(reader); break;
					case 4:
						len = reader.ReadInt32();
						Data = reader.ReadBytes(len);
						break;
					case 5: OutputTarget = reader.ReadByte(); break;
					case 6: Volume = reader.ReadInt32(); break;
					case 7: Balance = reader.ReadInt32(); break;
					case 8: Fade = reader.ReadInt32(); break;
					case 9: Volume = reader.ReadInt32(); break;
				}
			}
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write(Encoding.ASCII.GetBytes(Name).Length);
			writer.Write(Encoding.ASCII.GetBytes(Name));

			writer.Write(Encoding.ASCII.GetBytes(Path).Length);
			writer.Write(Encoding.ASCII.GetBytes(Path));

			writer.Write(Encoding.ASCII.GetBytes(InternalName).Length);
			writer.Write(Encoding.ASCII.GetBytes(InternalName));

			Wfx.Write(writer);

			writer.Write(Data.Length);
			writer.Write(Data);

			writer.Write(OutputTarget);
			writer.Write(Volume);
			writer.Write(Balance);
			writer.Write(Fade);
			writer.Write(Volume);
		}
	}
}
