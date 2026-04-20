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

namespace VisualPinball.Unity
{
	// Normalization helpers for matching materials between the exporter (Editor-side Materials)
	// and the importer (glTF-imported Materials that may carry a " (Instance)" suffix).
	public static class VpeMaterialNameUtil
	{
		public static string NormalizeMaterialName(string materialName)
		{
			if (string.IsNullOrWhiteSpace(materialName)) {
				return string.Empty;
			}

			const string instanceSuffix = " (Instance)";
			return materialName.EndsWith(instanceSuffix, StringComparison.Ordinal)
				? materialName[..^instanceSuffix.Length]
				: materialName;
		}

		public static string NormalizeTextureName(string textureName)
		{
			if (string.IsNullOrWhiteSpace(textureName)) {
				return string.Empty;
			}

			var name = textureName.Trim().ToLowerInvariant();
			if (name.EndsWith(".png", StringComparison.Ordinal)) {
				name = name[..^4];
			} else if (name.EndsWith(".jpg", StringComparison.Ordinal)) {
				name = name[..^4];
			} else if (name.EndsWith(".jpeg", StringComparison.Ordinal)) {
				name = name[..^5];
			} else if (name.EndsWith(".tga", StringComparison.Ordinal)) {
				name = name[..^4];
			} else if (name.EndsWith(".exr", StringComparison.Ordinal)) {
				name = name[..^4];
			}

			const string instanceSuffix = " (instance)";
			if (name.EndsWith(instanceSuffix, StringComparison.Ordinal)) {
				name = name[..^instanceSuffix.Length];
			}

			return name.Replace(" ", string.Empty);
		}
	}
}
