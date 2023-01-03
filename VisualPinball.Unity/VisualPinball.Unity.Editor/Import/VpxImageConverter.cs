﻿// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity.Editor
{
	public static class VpxImageConverter
	{
		public static void WriteAsAsset(this Texture texture, string folder, bool skipIfExists)
		{
			var path = texture.GetUnityFilename(folder);
			if (skipIfExists && File.Exists(path)) {
				return;
			}

			// convert if bmp
			if (texture.ConvertToPng) {
				using var im = texture.GetImage();
				im.Pngsave(path);

			} else if (texture.IsWebp) {
				// write both original and png conversion
				File.WriteAllBytes(path, texture.Content);

				using var im = texture.GetImage();
				im.Pngsave(Path.Combine(
					Path.GetDirectoryName(path) ?? "",
					Path.GetFileNameWithoutExtension(path) + ".png"
				));

			} else { // might need to convert other formats like webp
				File.WriteAllBytes(path, texture.Content);
			}
		}
	}
}
