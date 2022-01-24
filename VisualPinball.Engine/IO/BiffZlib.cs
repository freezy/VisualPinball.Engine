// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

namespace VisualPinball.Engine.IO
{
	/// <summary>
	/// Handles the ZLib compression sometimes used in BIFF records.
	/// </summary>
	public static class BiffZlib
	{
		public static byte[] Decompress(byte[] bytes)
		{
			using (var outStream = new MemoryStream())
			using (var inStream = new MemoryStream(bytes)) {
				NetMiniZ.NetMiniZ.Decompress(inStream, outStream);
				return outStream.ToArray();
			}
		}

		public static byte[] Compress(byte[] inData)
		{
			using (var outStream = new MemoryStream())
			using (var inStream = new MemoryStream(inData)) {
				NetMiniZ.NetMiniZ.Compress(inStream, outStream, 9);
				return outStream.ToArray();
			}
		}
	}
}
