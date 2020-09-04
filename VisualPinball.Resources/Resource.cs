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

using System.Reflection;

namespace VisualPinball.Resources
{
	public class Resource
	{
		public static readonly Resource BallDebug = new Resource("__BallDebug", GetTexture("VisualPinball.Resources.Textures.BallDebug.png"));
		public static readonly Resource BumperBase = new Resource("__BumperBase", GetTexture("VisualPinball.Resources.Textures.BumperBase.png"));
		public static readonly Resource BumperCap = new Resource("__BumperCap", GetTexture("VisualPinball.Resources.Textures.BumperCap.png"));
		public static readonly Resource BumperRing = new Resource("__BumperRing", GetTexture("VisualPinball.Resources.Textures.BumperRing.png"));
		public static readonly Resource BumperSocket = new Resource("__BumperSocket", GetTexture("VisualPinball.Resources.Textures.BumperSkirt.png"));

		public readonly string Name;
		public readonly byte[] Data;

		private Resource(string name, byte[] data)
		{
			Name = name;
			Data = data;
		}

		private static byte[] GetTexture(string name)
		{
			var a = Assembly.GetExecutingAssembly();
			using (var stream = a.GetManifestResourceStream(name)) {
				if (stream == null) {
					return null;
				}
				var ba = new byte[stream.Length];
				stream.Read(ba, 0, ba.Length);
				return ba;
			}
		}
	}
}
