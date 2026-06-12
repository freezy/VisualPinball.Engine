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

using System.Collections.Generic;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	// Editor-side extension point for rebuilding authoring materials from the vpe.material v1
	// payload during .vpe import. Pipeline packages (HDRP/URP) register an implementation at
	// editor load time; the package reader feeds it the imported texture assets.
	public interface IVpeMaterialV1EditorImporter
	{
		/// <summary>
		/// Builds materials from the payload's profiles, saves them as assets under
		/// <paramref name="materialAssetFolder"/> (project-relative, e.g.
		/// "Assets/Resources/Table/Materials") and assigns them to matching renderers under
		/// <paramref name="tableRoot"/>. Also applies the payload's renderer states.
		/// </summary>
		/// <returns>Number of material slots that received a rebuilt material.</returns>
		int Apply(
			Transform tableRoot,
			VpeMaterialsPayloadV1 payload,
			IReadOnlyDictionary<string, Texture2D> texturesById,
			string materialAssetFolder);
	}

	public static class VpeMaterialV1EditorImport
	{
		private static IVpeMaterialV1EditorImporter _active;

		public static void Register(IVpeMaterialV1EditorImporter importer)
		{
			_active = importer;
		}

		public static IVpeMaterialV1EditorImporter Active => _active;
	}
}
