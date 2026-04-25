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
	// Editor-side extension point for translating authoring materials into vpe.material v1 payload.
	// Pipeline packages (HDRP/URP/custom) register an implementation at editor load time.
	public interface IVpeMaterialV1Translator
	{
		VpeMaterialCaptureResult Capture(Transform tableRoot, IEnumerable<Renderer> renderers);
	}

	public readonly struct VpeMaterialCaptureResult
	{
		public VpeMaterialCaptureResult(VpeMaterialsPayloadV1 payload, IReadOnlyDictionary<string, byte[]> textureBlobs)
		{
			Payload = payload;
			TextureBlobs = textureBlobs;
		}

		public VpeMaterialsPayloadV1 Payload { get; }

		// Maps texture file name (matches VpeTextureAssetV1.FileName) to its PNG bytes.
		public IReadOnlyDictionary<string, byte[]> TextureBlobs { get; }
	}

	public static class VpeMaterialV1Translator
	{
		private static IVpeMaterialV1Translator _active;

		public static void Register(IVpeMaterialV1Translator translator)
		{
			_active = translator;
		}

		public static IVpeMaterialV1Translator Active => _active;

		public static VpeMaterialCaptureResult Capture(Transform tableRoot, IEnumerable<Renderer> renderers)
		{
			return _active == null
				? default
				: _active.Capture(tableRoot, renderers);
		}
	}
}
