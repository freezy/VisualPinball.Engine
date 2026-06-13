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
using System.Collections.Generic;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	// Editor-side extension point for translating authoring materials into the portable material
	// payload (schema v2). Pipeline packages (HDRP/URP/custom) register an implementation at
	// editor load time.
	public interface IVpeMaterialTranslator
	{
		/// <param name="tableRoot">Root of the exported table.</param>
		/// <param name="renderers">All renderers under the root.</param>
		/// <param name="nodeId">Resolves a transform to its stable package node id (null when the
		/// transform is not part of the export).</param>
		VpeMaterialCaptureResult Capture(Transform tableRoot, IEnumerable<Renderer> renderers, Func<Transform, string> nodeId);
	}

	// Optional editor-side hook that lets a pipeline package strip VPE-managed textures from the
	// temporary glTF export materials. This removes duplicate bytes from table.glb while keeping the
	// authored scene materials untouched.
	public interface IVpeMaterialGltfExportPreprocessor
	{
		IDisposable PrepareGltfExport(IEnumerable<Renderer> renderers);
	}

	public readonly struct VpeMaterialCaptureResult
	{
		public VpeMaterialCaptureResult(VpeMaterialsPayload payload, IReadOnlyList<VpeTextureBlobSource> textureBlobSources)
		{
			Payload = payload;
			TextureBlobSources = textureBlobSources;
		}

		public VpeMaterialsPayload Payload { get; }

		// Deferred sources for each texture's bytes (file path or inline bytes). The caller loads
		// them — typically in parallel on worker threads — into the final byte blobs; see
		// VpeTextureBlobLoader. Kept deferred so the heavy disk + PNG16→8 work stays off the main thread.
		public IReadOnlyList<VpeTextureBlobSource> TextureBlobSources { get; }
	}

	public static class VpeMaterialTranslator
	{
		private static IVpeMaterialTranslator _active;

		public static void Register(IVpeMaterialTranslator translator)
		{
			_active = translator;
		}

		public static IVpeMaterialTranslator Active => _active;

		public static VpeMaterialCaptureResult Capture(Transform tableRoot, IEnumerable<Renderer> renderers, Func<Transform, string> nodeId)
		{
			return _active == null
				? default
				: _active.Capture(tableRoot, renderers, nodeId);
		}

		public static IDisposable PrepareGltfExport(IEnumerable<Renderer> renderers)
		{
			return _active is IVpeMaterialGltfExportPreprocessor preprocessor
				? preprocessor.PrepareGltfExport(renderers)
				: null;
		}
	}
}
