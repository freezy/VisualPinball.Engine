// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

namespace VisualPinball.Engine.IO.FuturePinball
{
	/// <summary>
	/// Bounded decoder for the raw LZO1X streams used by Future Pinball's zLZO wrapper.
	/// The algorithm follows minilzo's lzo1x_decompress_safe state machine.
	/// </summary>
	public static class FuturePinballCompression
	{
		private static readonly byte[] Signature = { (byte)'z', (byte)'L', (byte)'Z', (byte)'O' };

		public static FuturePinballCompressedData Decode(
			ReadOnlyMemory<byte> raw,
			int maximumOutputBytes = FuturePinballReaderOptions.DefaultMaximumDecompressedBytes,
			string sourceName = null,
			long sourceOffset = -1)
		{
			if (!HasSignature(raw.Span)) {
				return new FuturePinballCompressedData {
					RawBytes = raw,
					DecodedBytes = raw.ToArray(),
					DeclaredUncompressedLength = raw.Length,
					CompressedBytesConsumed = raw.Length
				};
			}

			if (raw.Length < 8) {
				throw new FuturePinballFormatException("Truncated zLZO header", sourceName, sourceOffset);
			}

			var declaredLength = ReadInt32(raw.Span, 4);
			if (declaredLength < 0 || declaredLength > maximumOutputBytes) {
				throw new FuturePinballFormatException(
					$"zLZO output length {declaredLength} exceeds the configured limit {maximumOutputBytes}",
					sourceName,
					sourceOffset + 4
				);
			}

			var input = raw.Slice(8).Span;
			var output = new byte[declaredLength];
			try {
				var consumed = DecodeLzo1X(input, output);
				return new FuturePinballCompressedData {
					RawBytes = raw,
					IsCompressed = true,
					DeclaredUncompressedLength = declaredLength,
					CompressedBytesConsumed = consumed,
					DecodedBytes = output
				};
			} catch (FuturePinballFormatException) {
				throw;
			} catch (Exception exception) {
				throw new FuturePinballFormatException("Invalid LZO1X stream", sourceName, sourceOffset + 8, exception);
			}
		}

		private static bool HasSignature(ReadOnlySpan<byte> data)
		{
			return data.Length >= Signature.Length
				&& data[0] == Signature[0]
				&& data[1] == Signature[1]
				&& data[2] == Signature[2]
				&& data[3] == Signature[3];
		}

		private static int ReadInt32(ReadOnlySpan<byte> data, int offset)
		{
			return data[offset]
				| data[offset + 1] << 8
				| data[offset + 2] << 16
				| data[offset + 3] << 24;
		}

		private static int DecodeLzo1X(ReadOnlySpan<byte> source, Span<byte> destination)
		{
			var input = 0;
			var output = 0;
			var value = ReadByte(source, ref input);

			if (value > 17) {
				value -= 17;
				CopyLiteral(source, ref input, destination, ref output, value);
				value = ReadByte(source, ref input);
				if (value < 16) {
					throw new FuturePinballFormatException("Invalid initial LZO1X literal run");
				}
			}

			input--;
			while (true) {
				value = ReadByte(source, ref input);
				if (value < 16) {
					if (value == 0) {
						while (PeekByte(source, input) == 0) {
							value += 255;
							input++;
						}
						value += 15 + ReadByte(source, ref input);
					}

					value += 3;
					CopyLiteral(source, ref input, destination, ref output, value);
					value = ReadByte(source, ref input);
					if (value < 16) {
						var match = output - 0x801 - (value >> 2) - (ReadByte(source, ref input) << 2);
						CopyMatch(destination, ref output, match, 3);
						value = source[input - 2] & 3;
						if (value == 0) {
							continue;
						}
						CopyLiteral(source, ref input, destination, ref output, value);
						value = ReadByte(source, ref input);
					}
				}

				input--;
				while (true) {
					value = ReadByte(source, ref input);
					int match;
					if (value >= 64) {
						match = output - 1 - ((value >> 2) & 7) - (ReadByte(source, ref input) << 3);
						value = (value >> 5) - 1;
					} else if (value >= 32) {
						value &= 31;
						if (value == 0) {
							while (PeekByte(source, input) == 0) {
								value += 255;
								input++;
							}
							value += 31 + ReadByte(source, ref input);
						}
						match = output - 1 - (ReadByte(source, ref input) >> 2);
						match -= ReadByte(source, ref input) << 6;
					} else if (value >= 16) {
						match = output - ((value & 8) << 11);
						value &= 7;
						if (value == 0) {
							while (PeekByte(source, input) == 0) {
								value += 255;
								input++;
							}
							value += 7 + ReadByte(source, ref input);
						}
						match -= ReadByte(source, ref input) >> 2;
						match -= ReadByte(source, ref input) << 6;
						if (match == output) {
							if (value != 1 || output != destination.Length) {
								throw new FuturePinballFormatException(
									$"LZO1X terminator produced {output} of {destination.Length} expected bytes"
								);
							}
							return input;
						}
						match -= 0x4000;
					} else {
						match = output - 1 - (value >> 2) - (ReadByte(source, ref input) << 2);
						value = 0;
					}

					CopyMatch(destination, ref output, match, value + 2);
					value = source[input - 2] & 3;
					if (value == 0) {
						break;
					}
					CopyLiteral(source, ref input, destination, ref output, value);
				}
			}
		}

		private static int ReadByte(ReadOnlySpan<byte> source, ref int offset)
		{
			if ((uint)offset >= (uint)source.Length) {
				throw new FuturePinballFormatException("LZO1X input overrun");
			}
			return source[offset++];
		}

		private static int PeekByte(ReadOnlySpan<byte> source, int offset)
		{
			if ((uint)offset >= (uint)source.Length) {
				throw new FuturePinballFormatException("LZO1X input overrun");
			}
			return source[offset];
		}

		private static void CopyLiteral(ReadOnlySpan<byte> source, ref int input, Span<byte> destination, ref int output, int count)
		{
			if (count < 0 || input > source.Length - count) {
				throw new FuturePinballFormatException("LZO1X literal input overrun");
			}
			if (output > destination.Length - count) {
				throw new FuturePinballFormatException("LZO1X output overrun");
			}
			source.Slice(input, count).CopyTo(destination.Slice(output, count));
			input += count;
			output += count;
		}

		private static void CopyMatch(Span<byte> destination, ref int output, int match, int count)
		{
			if (count < 0 || match < 0 || match >= output) {
				throw new FuturePinballFormatException("LZO1X look-behind overrun");
			}
			if (output > destination.Length - count) {
				throw new FuturePinballFormatException("LZO1X output overrun");
			}
			for (var i = 0; i < count; i++) {
				destination[output++] = destination[match++];
			}
		}
	}
}
