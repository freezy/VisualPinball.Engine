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
using NLog;
using UnityEngine;
using UnityEngine.Rendering;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	// Runtime reader for the v1 material interchange. Owns the texture provider cache for one
	// import. Has no knowledge of HDRP, URP, or any SRP — the concrete material creation goes
	// through IVpeMaterialResolver.Active, which the Player registers at bootstrap.
	public static class VpeMaterialV1Reader
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static bool TryApply(IPackageFolder metaFolder, Transform tableRoot)
		{
			if (metaFolder == null || !tableRoot) {
				return false;
			}

			if (!metaFolder.TryGetFile(PackageApi.MaterialsV1File, out var payloadFile, PackageApi.Packer.FileExtension)) {
				return false;
			}

			VpeMaterialsPayloadV1 payload;
			try {
				payload = PackageApi.Packer.Unpack<VpeMaterialsPayloadV1>(payloadFile.GetData());
			} catch (Exception e) {
				Logger.Warn(e, "Failed to parse materials.v1 payload. Falling back to legacy path.");
				return false;
			}

			if (payload == null || payload.Profiles == null || payload.Profiles.Length == 0) {
				return false;
			}
			if (payload.FormatVersion != 1) {
				Logger.Warn(
					$"materials.v1 declares FormatVersion={payload.FormatVersion} which this reader does not " +
					"understand. Falling back to legacy path.");
				return false;
			}

			var resolver = VpeMaterialResolver.Active;
			if (resolver == null) {
				Logger.Warn(
					"v1 material payload present but no IVpeMaterialResolver is registered. The Player app " +
					"must register a resolver at startup. Falling back to glTF-imported materials (visuals " +
					"will not match authoring).");
				return false;
			}

			var profilesByName = BuildProfileLookup(payload.Profiles);
			metaFolder.TryGetFolder(PackageApi.TexturesV1Folder, out var texturesFolder);
			using var textures = new TextureProvider(payload.Textures, texturesFolder);

			var stats = new Stats();
			foreach (var renderer in tableRoot.GetComponentsInChildren<Renderer>(true)) {
				if (!renderer) {
					continue;
				}

				var materials = renderer.sharedMaterials;
				var modified = false;
				for (var i = 0; i < materials.Length; i++) {
					var imported = materials[i];
					if (!imported) {
						continue;
					}
					stats.TotalSlots++;

					var key = NormalizeMaterialName(imported.name);
					if (!profilesByName.TryGetValue(key, out var profile)) {
						stats.UnmatchedNames.Add(key);
						continue;
					}
					stats.MatchedSlots++;

					if (!resolver.Supports(profile.Type)) {
						stats.UnsupportedTypes.Add(profile.Type);
						continue;
					}

					var replacement = resolver.CreateMaterial(profile, textures, imported);
					if (!replacement) {
						stats.ResolverReturnedNull++;
						continue;
					}
					materials[i] = replacement;
					modified = true;
					stats.AppliedSlots++;
				}

				if (modified) {
					renderer.sharedMaterials = materials;
				}
			}

			// Apply per-renderer state (shadowCastingMode, receiveShadows, renderingLayerMask) that
			// glTF doesn't carry. Paths are resolved through FindByPath so the existing
			// SparsePathIndexMap handles any sibling-index shift introduced by the glTF round-trip.
			if (payload.RendererStates != null) {
				foreach (var state in payload.RendererStates) {
					if (state == null || string.IsNullOrEmpty(state.Path)) {
						continue;
					}
					var target = tableRoot.FindByPath(state.Path);
					if (!target) {
						stats.RendererStatesMissing++;
						continue;
					}
					if (!target.TryGetComponent<Renderer>(out var renderer)) {
						stats.RendererStatesMissing++;
						continue;
					}
					ApplyRendererState(renderer, state);
					stats.RendererStatesApplied++;
				}
			}

			if (stats.UnmatchedNames.Count > 0) {
				var sample = string.Join(", ", TakeFirst(stats.UnmatchedNames, 12));
				Logger.Warn($"vpe.material v1 unmatched material-name sample: {sample}");
			}
			// Logged at Warn level during development so the summary survives Unity's console ring
			// buffer alongside the resolver's per-material warnings. Drop to Info once the v1
			// interchange is stable.
			Logger.Warn(
				$"vpe.material v1 applied: profiles={payload.Profiles.Length}, textures={payload.Textures?.Length ?? 0}, " +
				$"slots={stats.TotalSlots}, matched={stats.MatchedSlots}, applied={stats.AppliedSlots}, " +
				$"rendererStates={stats.RendererStatesApplied}/{payload.RendererStates?.Length ?? 0} (missing at import={stats.RendererStatesMissing}), " +
				$"resolverNull={stats.ResolverReturnedNull}, unsupportedTypes={stats.UnsupportedTypes.Count}, " +
				$"unmatched={stats.UnmatchedNames.Count}.");

			return true;
		}

		private static IEnumerable<string> TakeFirst(IEnumerable<string> source, int count)
		{
			var i = 0;
			foreach (var item in source) {
				yield return item;
				i++;
				if (i >= count) {
					yield break;
				}
			}
		}

		private static Dictionary<string, VpeMaterialProfileV1> BuildProfileLookup(VpeMaterialProfileV1[] profiles)
		{
			var lookup = new Dictionary<string, VpeMaterialProfileV1>(profiles.Length, StringComparer.Ordinal);
			foreach (var profile in profiles) {
				if (profile == null || string.IsNullOrWhiteSpace(profile.Name)) {
					continue;
				}
				lookup[NormalizeMaterialName(profile.Name)] = profile;
			}
			return lookup;
		}

		private static void ApplyRendererState(Renderer renderer, VpeRendererStateV1 state)
		{
			renderer.shadowCastingMode = (ShadowCastingMode)state.ShadowCastingMode;
			renderer.receiveShadows = state.ReceiveShadows;
			renderer.renderingLayerMask = state.RenderingLayerMask;
		}

		private static string NormalizeMaterialName(string name) => VpeMaterialNameUtil.NormalizeMaterialName(name);

		private sealed class Stats
		{
			public int TotalSlots;
			public int MatchedSlots;
			public int AppliedSlots;
			public int ResolverReturnedNull;
			public int RendererStatesApplied;
			public int RendererStatesMissing;
			public readonly HashSet<string> UnmatchedNames = new(StringComparer.Ordinal);
			public readonly HashSet<string> UnsupportedTypes = new(StringComparer.Ordinal);
		}

		private sealed class TextureProvider : IVpeTextureProvider, IDisposable
		{
			private readonly Dictionary<string, VpeTextureAssetV1> _assetsById;
			private readonly IPackageFolder _folder;
			private readonly Dictionary<string, Texture2D> _loaded = new(StringComparer.Ordinal);
			private readonly HashSet<string> _missingTextureIdsLogged = new(StringComparer.Ordinal);

			public TextureProvider(VpeTextureAssetV1[] assets, IPackageFolder folder)
			{
				_folder = folder;
				_assetsById = new Dictionary<string, VpeTextureAssetV1>(StringComparer.Ordinal);
				if (assets == null) {
					return;
				}
				foreach (var asset in assets) {
					if (asset == null || string.IsNullOrWhiteSpace(asset.Id)) {
						continue;
					}
					_assetsById[asset.Id] = asset;
				}
			}

			public Texture2D Get(string textureId)
			{
				if (string.IsNullOrWhiteSpace(textureId)) {
					return null;
				}
				if (_loaded.TryGetValue(textureId, out var cached)) {
					return cached;
				}
				if (!_assetsById.TryGetValue(textureId, out var asset) || _folder == null) {
					LogMissingTexture(textureId, reason: "id-not-in-payload-or-texture-folder-missing");
					_loaded[textureId] = null;
					return null;
				}
				if (!_folder.TryGetFile(asset.FileName, out var file)) {
					LogMissingTexture(textureId, reason: $"file-not-found:{asset.FileName}");
					_loaded[textureId] = null;
					return null;
				}

				var bytes = file.GetData();
				if (bytes == null || bytes.Length == 0) {
					LogMissingTexture(textureId, reason: $"file-empty:{asset.FileName}");
					_loaded[textureId] = null;
					return null;
				}

				var linear = string.Equals(asset.ColorSpace, VpeColorSpaces.Linear, StringComparison.OrdinalIgnoreCase);
				var texture = new Texture2D(2, 2, TextureFormat.RGBA32, asset.GenerateMipMaps, linear) {
					name = string.IsNullOrWhiteSpace(asset.SourceName) ? asset.Id : asset.SourceName,
				};
				if (!ImageConversion.LoadImage(texture, bytes, markNonReadable: true)) {
					UnityEngine.Object.Destroy(texture);
					LogMissingTexture(textureId, reason: $"load-image-failed:{asset.FileName}");
					_loaded[textureId] = null;
					return null;
				}
				texture.wrapMode = (TextureWrapMode)asset.WrapMode;
				texture.filterMode = (FilterMode)asset.FilterMode;
				texture.anisoLevel = Mathf.Max(1, asset.AnisoLevel);
				_loaded[textureId] = texture;
				return texture;
			}

			public void Dispose()
			{
				// Textures are handed to materials on the instantiated table. The table owns them from
				// here on; disposing would destroy still-referenced textures. Intentionally no-op.
				_loaded.Clear();
			}

			private void LogMissingTexture(string textureId, string reason)
			{
				if (!_missingTextureIdsLogged.Add(textureId)) {
					return;
				}
				Logger.Warn($"vpe.material v1 texture lookup failed for TextureId='{textureId}' ({reason}).");
			}
		}
	}
}
