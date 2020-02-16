// ReSharper disable FieldCanBeMadeReadOnly.Global

using System;
using System.IO;

namespace VisualPinball.Engine.VPT.Sound
{
	[Serializable]
	public class WaveFormat
	{
		/// <summary>
		/// Format type
		/// </summary>
		public ushort FormatTag;

		/// <summary>
		/// Number of channels (i.e. mono, stereo...)
		/// </summary>
		public ushort Channels;

		/// <summary>
		/// Sample rate
		/// </summary>
		public uint SamplesPerSec;

		/// <summary>
		/// For buffer estimation
		/// </summary>
		public uint AvgBytesPerSec;

		/// <summary>
		/// Block size of data
		/// </summary>
		public ushort BlockAlign;

		/// <summary>
		/// Number of bits per sample of mono data
		/// </summary>
		public ushort BitsPerSample;

		/// <summary>
		/// The count in bytes of the size of extra information (after cbSize)
		/// </summary>
		public ushort CbSize;

		public WaveFormat(BinaryReader reader) {
			FormatTag = reader.ReadUInt16();
			Channels = reader.ReadUInt16();
			SamplesPerSec = reader.ReadUInt32();
			AvgBytesPerSec = reader.ReadUInt32();
			BlockAlign = reader.ReadUInt16();
			BitsPerSample = reader.ReadUInt16();
			CbSize = reader.ReadUInt16();
		}

		public void Write(BinaryWriter writer)
		{
			writer.Write(FormatTag);
			writer.Write(Channels);
			writer.Write(SamplesPerSec);
			writer.Write(AvgBytesPerSec);
			writer.Write(BlockAlign);
			writer.Write(BitsPerSample);
			writer.Write(CbSize);
		}
	}
}
