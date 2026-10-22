// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using VisualPinball.Engine.VPT.Sound;

namespace VisualPinball.Unity
{
	/// <summary>Conservative exact/PCM identity. Unknown playback chunks never disappear in canonical matching.</summary>
	public static class VpeAudioIdentity
	{
		public static string Canonical(byte[] bytes)
		{
			return TryReadPcm(bytes, out var format, out var samples) ? Canonical(format, samples) : null;
		}

		public static string Canonical(WaveFormat format, byte[] samples)
		{
			if (!Valid(format, samples?.Length ?? -1)) return null;
			using var stream = new MemoryStream();
			using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
			writer.Write(Encoding.ASCII.GetBytes("VPE-AUDIO-PCM\0\x01"));
			format.Write(writer); writer.Write(samples.Length / format.BlockAlign); writer.Flush();
			var prefix = stream.ToArray();
			using var hash = SHA256.Create();
			hash.TransformBlock(prefix, 0, prefix.Length, prefix, 0);
			hash.TransformBlock(samples, 0, samples.Length, samples, 0);
			hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
			VpeIntegrity.BytesHashed?.Invoke(prefix.LongLength + samples.LongLength);
			return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
		}

		private static bool Valid(WaveFormat f, int length) => f != null && f.FormatTag == 1 && f.CbSize == 0 && f.Channels >= 1 && f.Channels <= 2 && f.SamplesPerSec > 0 && f.SamplesPerSec <= 384000 && (f.BitsPerSample == 8 || f.BitsPerSample == 16 || f.BitsPerSample == 24 || f.BitsPerSample == 32) && f.BlockAlign == f.Channels * (f.BitsPerSample / 8) && f.AvgBytesPerSec == f.SamplesPerSec * f.BlockAlign && length >= 0 && length % f.BlockAlign == 0;

		public static bool TryReadPcm(byte[] bytes, out WaveFormat format, out byte[] samples)
		{
			format = null; samples = null;
			if (bytes == null || bytes.Length < 12) return false;
			using var stream = new MemoryStream(bytes, false);
			using var reader = new BinaryReader(stream);
			string Four() => Encoding.ASCII.GetString(reader.ReadBytes(4));
			if (Four() != "RIFF" || reader.ReadUInt32() != bytes.Length - 8 || Four() != "WAVE") return false;
			while (stream.Position < stream.Length) {
				if (stream.Length - stream.Position < 8) return false;
				var chunk = Four(); var length = reader.ReadUInt32(); var end = stream.Position + length;
				if (end > stream.Length || length > int.MaxValue) return false;
				switch (chunk) {
					case "fmt ":
						if (format != null || length != 16 && length != 18) return false;
						format = new WaveFormat { FormatTag = reader.ReadUInt16(), Channels = reader.ReadUInt16(), SamplesPerSec = reader.ReadUInt32(), AvgBytesPerSec = reader.ReadUInt32(), BlockAlign = reader.ReadUInt16(), BitsPerSample = reader.ReadUInt16(), CbSize = length == 18 ? reader.ReadUInt16() : (ushort)0 };
						break;
					case "data": if (samples != null) return false; samples = reader.ReadBytes((int)length); break;
					case "LIST": if (length < 4 || Four() != "INFO") return false; break;
					case "JUNK": case "PAD ": break;
					default: return false;
				}
				// VPE's historical WAV writer omits an odd final chunk's pad byte.
				// Accept that terminal case only; interior chunks still require their padding.
				stream.Position = end == stream.Length ? end : end + (length & 1);
				if (stream.Position > stream.Length) return false;
			}
			return samples != null && Valid(format, samples.Length);
		}
	}
}
