// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

namespace VisualPinball.Engine.VPT
{
	public static class MaterialReader
	{
		public static Material[] Load(string filename)
		{
			if (!File.Exists(filename)) {
				throw new ArgumentException($"File \"{filename}\" does not exist");
			}

			using (var reader = new BinaryReader(File.Open(filename, FileMode.Open))) {

				var version = reader.ReadInt32();
				if (version != 1) {
					throw new InvalidDataException("Materials are not compatible with this version.");
				}

				var materialCount = reader.ReadInt32();
				var materials = new Material[materialCount];
				for (var i = 0; i < materialCount; i++) {
					materials[i] = new Material(reader) {
						Elasticity = reader.ReadSingle(),
						ElasticityFalloff = reader.ReadSingle(),
						Friction = reader.ReadSingle(),
						ScatterAngle = reader.ReadSingle()
					};
				}
				return materials;
			}
		}
	}
}
