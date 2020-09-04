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

		public WaveFormat()
		{
			FormatTag = 1;
			Channels = 1;
			SamplesPerSec = 44100;
			AvgBytesPerSec = 88200;
			BlockAlign = 2;
			BitsPerSample = 16;
			CbSize = 0;
		}

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
