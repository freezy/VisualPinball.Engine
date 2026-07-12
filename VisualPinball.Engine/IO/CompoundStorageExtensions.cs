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
using System.IO;

using OpenMcdf;

namespace VisualPinball.Engine.IO
{
	internal static class CompoundStorageExtensions
	{
		public static byte[] ReadAll(this CfbStream stream)
		{
			if (stream.Length > int.MaxValue) {
				throw new InvalidDataException($"Compound stream is too large to load ({stream.Length} bytes).");
			}

			stream.Position = 0;
			var data = new byte[(int)stream.Length];
			var offset = 0;
			while (offset < data.Length) {
				var read = stream.Read(data, offset, data.Length - offset);
				if (read == 0) {
					throw new EndOfStreamException($"Compound stream ended after {offset} of {data.Length} bytes.");
				}
				offset += read;
			}
			return data;
		}

		public static void WriteAll(this CfbStream stream, byte[] data)
		{
			if (data == null) {
				throw new ArgumentNullException(nameof(data));
			}
			stream.SetLength(data.Length);
			stream.Position = 0;
			stream.Write(data, 0, data.Length);
			stream.Flush();
		}
	}
}
