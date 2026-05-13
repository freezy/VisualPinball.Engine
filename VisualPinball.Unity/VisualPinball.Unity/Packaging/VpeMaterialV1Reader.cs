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
using System.Diagnostics;
using System.Text;
using NLog;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
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

			return TryApply(payload, embeddedTextureBlobsById: null, metaFolder, tableRoot, PackageApi.MaterialsV1File);
		}

		public static bool TryApply(
			VpeMaterialsPayloadV1 payload,
			IReadOnlyDictionary<string, byte[]> embeddedTextureBlobsById,
			IPackageFolder metaFolder,
			Transform tableRoot,
			string sourceLabel)
		{
			if (payload == null || !tableRoot || payload.Profiles == null || payload.Profiles.Length == 0) {
				return false;
			}
			if (payload.FormatVersion != 1) {
				Logger.Warn(
					$"{sourceLabel} declares FormatVersion={payload.FormatVersion} which this reader does not " +
					"understand. Falling back to glTF-imported materials.");
				return false;
			}

			var resolver = VpeMaterialResolver.Active;
			if (resolver == null) {
				Logger.Warn(
					$"v1 material payload present in {sourceLabel} but no IVpeMaterialResolver is registered. The Player app " +
					"must register a resolver at startup. Falling back to glTF-imported materials (visuals " +
					"will not match authoring).");
				return false;
			}
			var resolverDiagnostics = resolver as IVpeMaterialResolverDiagnostics;
			resolverDiagnostics?.ResetDiagnostics();

			var profilesByName = BuildProfileLookup(payload.Profiles);
			var resolvedMaterialsByImportedId = new Dictionary<int, Material>();
			var resolvedMaterialsBySignature = new Dictionary<string, Material>(StringComparer.Ordinal);
			byte[] packedTextureData = null;
			if (metaFolder != null && metaFolder.TryGetFile(PackageApi.TexturesV1PackFile, out var packedTexturesFile)) {
				packedTextureData = packedTexturesFile.GetData();
			}
			using var textures = new TextureProvider(payload.Textures, packedTextureData, embeddedTextureBlobsById);

			var stats = new Stats();
			var materialTraversalStopwatch = Stopwatch.StartNew();
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

					var importedMaterialId = imported.GetInstanceID();
					if (resolvedMaterialsByImportedId.TryGetValue(importedMaterialId, out var cachedReplacement)) {
						materials[i] = cachedReplacement;
						modified = true;
						stats.AppliedSlots++;
						stats.ReusedResolvedMaterials++;
						continue;
					}

					var semanticCacheKey = BuildResolvedMaterialCacheKey(profile, imported);
					if (semanticCacheKey != null
						&& resolvedMaterialsBySignature.TryGetValue(semanticCacheKey, out var signatureCachedReplacement)) {
						resolvedMaterialsByImportedId[importedMaterialId] = signatureCachedReplacement;
						materials[i] = signatureCachedReplacement;
						modified = true;
						stats.AppliedSlots++;
						stats.ReusedResolvedMaterials++;
						stats.ReusedResolvedMaterialSignatures++;
						continue;
					}

					var resolverStopwatch = Stopwatch.StartNew();
					var replacement = resolver.CreateMaterial(profile, textures, imported);
					resolverStopwatch.Stop();
					stats.ResolverCreateMaterialMilliseconds += resolverStopwatch.ElapsedMilliseconds;
					if (!replacement) {
						stats.ResolverReturnedNull++;
						continue;
					}
					resolvedMaterialsByImportedId[importedMaterialId] = replacement;
					if (semanticCacheKey != null) {
						resolvedMaterialsBySignature[semanticCacheKey] = replacement;
					}
					materials[i] = replacement;
					modified = true;
					stats.AppliedSlots++;
					stats.CreatedResolvedMaterials++;
				}

				if (modified) {
					renderer.sharedMaterials = materials;
				}
			}
			materialTraversalStopwatch.Stop();
			stats.MaterialTraversalMilliseconds = materialTraversalStopwatch.ElapsedMilliseconds;

			// Apply per-renderer state (shadowCastingMode, receiveShadows, renderingLayerMask) that
			// glTF doesn't carry. Paths are resolved through FindByPath so the existing
			// SparsePathIndexMap handles any sibling-index shift introduced by the glTF round-trip.
			var rendererStateStopwatch = Stopwatch.StartNew();
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
			rendererStateStopwatch.Stop();
			stats.RendererStateMilliseconds = rendererStateStopwatch.ElapsedMilliseconds;

			if (stats.UnmatchedNames.Count > 0) {
				var sample = string.Join(", ", TakeFirst(stats.UnmatchedNames, 12));
				Logger.Warn($"vpe.material v1 unmatched material-name sample: {sample}");
			}
			// Logged at Warn level during development so the summary survives Unity's console ring
			// buffer alongside the resolver's per-material warnings. Drop to Info once the v1
			// interchange is stable.
			Logger.Warn(
				$"vpe.material v1 applied from {sourceLabel}: profiles={payload.Profiles.Length}, " +
				$"textures={payload.Textures?.Length ?? 0}, " +
				$"slots={stats.TotalSlots}, matched={stats.MatchedSlots}, applied={stats.AppliedSlots}, " +
				$"rendererStates={stats.RendererStatesApplied}/{payload.RendererStates?.Length ?? 0} (missing at import={stats.RendererStatesMissing}), " +
				$"materialTraversalMs={stats.MaterialTraversalMilliseconds}, resolverCreateMaterialMs={stats.ResolverCreateMaterialMilliseconds}, " +
				$"rendererStateMs={stats.RendererStateMilliseconds}, " +
				$"resolverNull={stats.ResolverReturnedNull}, unsupportedTypes={stats.UnsupportedTypes.Count}, " +
				$"unmatched={stats.UnmatchedNames.Count}, " +
				$"resolvedMaterialsCreated={stats.CreatedResolvedMaterials}, resolvedMaterialsReused={stats.ReusedResolvedMaterials}, " +
				$"resolvedMaterialsSignatureReused={stats.ReusedResolvedMaterialSignatures}, " +
				$"textureCacheHits={textures.CacheHits}, textureLoads={textures.LoadCount}, " +
				$"textureLoadMs={textures.LoadMilliseconds}, textureBytes={textures.LoadedBytes}, " +
				$"embeddedTextureLoads={textures.EmbeddedLoadCount}, packedTextureLoads={textures.PackedLoadCount}, " +
				$"resolverStats=[{resolverDiagnostics?.GetDiagnosticsSummary() ?? "n/a"}].");

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

		private static string BuildResolvedMaterialCacheKey(VpeMaterialProfileV1 profile, Material imported)
		{
			if (profile == null || !imported) {
				return null;
			}

			var texturePropertyNames = imported.GetTexturePropertyNames();
			Array.Sort(texturePropertyNames, StringComparer.Ordinal);

			var builder = new StringBuilder(256);
			builder.Append(profile.Type ?? string.Empty)
				.Append('|')
				.Append(imported.shader ? imported.shader.name : string.Empty);
			AppendProfileSemanticKey(builder, profile);

			foreach (var propertyName in texturePropertyNames) {
				if (string.IsNullOrWhiteSpace(propertyName)) {
					continue;
				}

				var texture = imported.GetTexture(propertyName);
				builder.Append('|')
					.Append(propertyName)
					.Append('=')
					.Append(texture ? texture.GetInstanceID() : 0);
			}

			return builder.ToString();
		}

		private static void AppendProfileSemanticKey(StringBuilder builder, VpeMaterialProfileV1 profile)
		{
			if (builder == null || profile == null) {
				return;
			}

			byte[] payload = null;
			switch (profile.Type) {
				case VpeMaterialTypes.Lit:
					if (profile.Lit != null) {
						payload = PackageApi.Packer.Pack(profile.Lit);
					}
					break;
				case VpeMaterialTypes.Decal:
					if (profile.Decal != null) {
						payload = PackageApi.Packer.Pack(profile.Decal);
					}
					break;
				case VpeMaterialTypes.Unlit:
					if (profile.Unlit != null) {
						payload = PackageApi.Packer.Pack(profile.Unlit);
					}
					break;
				case VpeMaterialTypes.Metal:
					if (profile.Metal != null) {
						payload = PackageApi.Packer.Pack(profile.Metal);
					}
					break;
				case VpeMaterialTypes.Rubber:
					if (profile.Rubber != null) {
						payload = PackageApi.Packer.Pack(profile.Rubber);
					}
					break;
				case VpeMaterialTypes.Dmd:
					if (profile.Dmd != null) {
						payload = PackageApi.Packer.Pack(profile.Dmd);
					}
					break;
			}

			if (payload == null || payload.Length == 0) {
				return;
			}

			builder.Append("|profile=")
				.Append(Encoding.UTF8.GetString(payload));
		}

		private static void ApplyRendererState(Renderer renderer, VpeRendererStateV1 state)
		{
			renderer.shadowCastingMode = (ShadowCastingMode)state.ShadowCastingMode;
			renderer.receiveShadows = state.ReceiveShadows;
			renderer.renderingLayerMask = state.RenderingLayerMask;
			if (state.RayTracingMode >= 0) {
				renderer.rayTracingMode = (RayTracingMode)state.RayTracingMode;
			}
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
			public int CreatedResolvedMaterials;
			public int ReusedResolvedMaterials;
			public int ReusedResolvedMaterialSignatures;
			public long MaterialTraversalMilliseconds;
			public long ResolverCreateMaterialMilliseconds;
			public long RendererStateMilliseconds;
			public readonly HashSet<string> UnmatchedNames = new(StringComparer.Ordinal);
			public readonly HashSet<string> UnsupportedTypes = new(StringComparer.Ordinal);
		}

		private sealed class TextureProvider : IVpeTextureProvider, IDisposable
		{
			private readonly Dictionary<string, VpeTextureAssetV1> _assetsById;
			private readonly IReadOnlyDictionary<string, byte[]> _embeddedTextureBlobsById;
			private readonly byte[] _packedTextureData;
			private readonly Dictionary<string, Texture2D> _loaded = new(StringComparer.Ordinal);
			private readonly HashSet<string> _missingTextureIdsLogged = new(StringComparer.Ordinal);
			private long _loadedBytes;
			private long _loadMilliseconds;
			private int _loadCount;
			private int _cacheHits;
			private int _embeddedLoadCount;
			private int _packedLoadCount;
			public TextureProvider(
				VpeTextureAssetV1[] assets,
				byte[] packedTextureData,
				IReadOnlyDictionary<string, byte[]> embeddedTextureBlobsById)
			{
				_packedTextureData = packedTextureData;
				_embeddedTextureBlobsById = embeddedTextureBlobsById;
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
					_cacheHits++;
					return cached;
				}
				if (!_assetsById.TryGetValue(textureId, out var asset)) {
					LogMissingTexture(textureId, reason: "id-not-in-payload");
					_loaded[textureId] = null;
					return null;
				}

				byte[] bytes = null;
				var loadedFromEmbedded = false;
				var loadedFromPacked = false;
				if (_embeddedTextureBlobsById != null) {
					_embeddedTextureBlobsById.TryGetValue(textureId, out bytes);
					loadedFromEmbedded = bytes != null && bytes.Length > 0;
				}
				if ((bytes == null || bytes.Length == 0) && TryReadPackedTextureBytes(asset, out var packedBytes)) {
					bytes = packedBytes;
					loadedFromPacked = true;
				}
				if (bytes == null || bytes.Length == 0) {
					string reason;
					if (asset.GlbBufferView >= 0) {
						reason = $"glb-bufferView-missing:{asset.GlbBufferView}";
					} else if (asset.ByteOffset >= 0 && asset.ByteLength > 0) {
						reason = $"packed-texture-range-missing:{asset.ByteOffset}+{asset.ByteLength}";
					} else {
						reason = $"no-embedded-or-packed-bytes:{asset.Id}";
					}
					LogMissingTexture(textureId, reason: reason);
					_loaded[textureId] = null;
					return null;
				}

				var linear = string.Equals(asset.ColorSpace, VpeColorSpaces.Linear, StringComparison.OrdinalIgnoreCase);
				var generateMipMaps = asset.GenerateMipMaps;
				var loadStopwatch = Stopwatch.StartNew();
				var texture = new Texture2D(2, 2, TextureFormat.RGBA32, generateMipMaps, linear) {
					name = string.IsNullOrWhiteSpace(asset.SourceName) ? asset.Id : asset.SourceName,
				};
				if (!ImageConversion.LoadImage(texture, bytes, markNonReadable: false)) {
					loadStopwatch.Stop();
					UnityEngine.Object.Destroy(texture);
					LogMissingTexture(textureId, reason: $"load-image-failed:{asset.FileName}");
					_loaded[textureId] = null;
					return null;
				}
				if (linear && asset.RuntimeCompress) {
					try {
						texture.Compress(highQuality: true);
					} catch (Exception ex) {
						Logger.Warn(ex, $"vpe.material v1 failed compressing linear texture '{texture.name}'. Keeping uncompressed texture.");
					}
				}
				texture.Apply(updateMipmaps: generateMipMaps, makeNoLongerReadable: true);
				loadStopwatch.Stop();
				texture.wrapMode = (TextureWrapMode)asset.WrapMode;
				texture.filterMode = (FilterMode)asset.FilterMode;
				texture.anisoLevel = Mathf.Max(1, asset.AnisoLevel);
				_loadCount++;
				_loadMilliseconds += loadStopwatch.ElapsedMilliseconds;
				_loadedBytes += bytes.LongLength;
				if (loadedFromEmbedded) {
					_embeddedLoadCount++;
				} else if (loadedFromPacked) {
					_packedLoadCount++;
				}
				_loaded[textureId] = texture;
				return texture;
			}

			public int LoadCount => _loadCount;
			public int CacheHits => _cacheHits;
			public long LoadMilliseconds => _loadMilliseconds;
			public long LoadedBytes => _loadedBytes;
			public int EmbeddedLoadCount => _embeddedLoadCount;
			public int PackedLoadCount => _packedLoadCount;

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

			private bool TryReadPackedTextureBytes(VpeTextureAssetV1 asset, out byte[] bytes)
			{
				bytes = null;
				if (asset == null
					|| _packedTextureData == null
					|| asset.ByteOffset < 0
					|| asset.ByteLength <= 0) {
					return false;
				}

				var endOffset = asset.ByteOffset + asset.ByteLength;
				if (endOffset > _packedTextureData.Length || endOffset < asset.ByteOffset) {
					return false;
				}

				bytes = new byte[asset.ByteLength];
				Buffer.BlockCopy(_packedTextureData, asset.ByteOffset, bytes, 0, asset.ByteLength);
				return true;
			}
		}
	}
}
