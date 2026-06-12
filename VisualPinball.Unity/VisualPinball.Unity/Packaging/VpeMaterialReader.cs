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
using System.Threading.Tasks;
using NLog;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	// Runtime reader for the material interchange. Owns the texture provider cache for one
	// import. Has no knowledge of HDRP, URP, or any SRP — the concrete material creation goes
	// through IVpeMaterialResolver.Active, which the Player registers at bootstrap.
	public static class VpeMaterialReader
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		// Yield to the player loop after roughly this many texture bytes were uploaded within one
		// frame. Hundreds of megabytes of GPU uploads queued in a single frame can starve the D3D12
		// queue badly enough to trip the Windows GPU watchdog (device removed), so the budget keeps
		// each frame's upload burst bounded while costing only a handful of frames in total.
		private const long UploadYieldBudgetBytes = 160L * 1024 * 1024;

		/// <summary>
		/// Loads the material payload of a package (meta/materials.json). When
		/// <paramref name="loadSourceBytes"/> is set, also loads the source texture layer
		/// (table/textures/ files); otherwise only the entry metadata is returned — the path
		/// taken when a cooked texture cache will provide the bytes.
		/// </summary>
		public static bool TryLoad(
			IPackageFolder tableFolder,
			bool loadSourceBytes,
			out VpeMaterialsPayload payload,
			out VpeTextureSources.Result sources)
		{
			payload = null;
			sources = null;
			if (tableFolder == null || !tableFolder.TryGetFolder(PackageApi.MetaFolder, out var metaFolder)) {
				return false;
			}

			if (!metaFolder.TryGetFile(PackageApi.MaterialsFile, out var payloadFile, PackageApi.Packer.FileExtension)) {
				return false;
			}

			try {
				payload = PackageApi.Packer.Unpack<VpeMaterialsPayload>(payloadFile.GetData());
			} catch (Exception e) {
				Logger.Warn(e, "Failed parsing materials payload.");
				return false;
			}
			if (payload == null) {
				return false;
			}
			if (payload.FormatVersion != VpeMaterialSchema.Version) {
				Logger.Warn(
					$"Materials payload declares FormatVersion={payload.FormatVersion} which this reader does not " +
					"understand. Falling back to glTF-imported materials.");
				payload = null;
				return false;
			}
			sources = loadSourceBytes
				? VpeTextureSources.Load(tableFolder, payload)
				: new VpeTextureSources.Result { Entries = VpeTextureSources.ToPayloads(payload.Textures) };
			return true;
		}

		public static async Task<bool> TryApplyAsync(
			VpeMaterialsPayload payload,
			VpeTexturePayload[] textureEntries,
			byte[] textureBlob,
			Transform tableRoot,
			Func<string, Transform> resolveNodeById,
			string sourceLabel)
		{
			if (payload == null || !tableRoot || payload.Profiles == null || payload.Profiles.Length == 0) {
				return false;
			}

			var resolver = VpeMaterialResolver.Active;
			if (resolver == null) {
				Logger.Warn(
					$"Material payload present in {sourceLabel} but no IVpeMaterialResolver is registered. The Player app " +
					"must register a resolver at startup. Falling back to glTF-imported materials (visuals " +
					"will not match authoring).");
				return false;
			}
			var resolverDiagnostics = resolver as IVpeMaterialResolverDiagnostics;
			resolverDiagnostics?.ResetDiagnostics();

			var profilesByName = BuildProfileLookup(payload.Profiles);
			var resolvedMaterialsByImportedId = new Dictionary<int, Material>();
			var resolvedMaterialsBySignature = new Dictionary<string, Material>(StringComparer.Ordinal);
			// The semantic part of the cache key serializes the whole profile; doing that once per
			// profile instead of once per material slot keeps the traversal cheap on big tables.
			var semanticKeysByProfile = new Dictionary<VpeMaterialProfile, string>();
			using var textures = new TextureProvider(textureEntries, textureBlob);

			var stats = new Stats();
			var materialTraversalStopwatch = Stopwatch.StartNew();
			var uploadedBytesAtLastYield = 0L;
			foreach (var renderer in tableRoot.GetComponentsInChildren<Renderer>(true)) {
				if (!renderer) {
					continue;
				}

				if (textures.LoadedBytes - uploadedBytesAtLastYield > UploadYieldBudgetBytes) {
					await Task.Yield();
					uploadedBytesAtLastYield = textures.LoadedBytes;
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

					if (!semanticKeysByProfile.TryGetValue(profile, out var profileSemanticKey)) {
						profileSemanticKey = BuildProfileSemanticKey(profile);
						semanticKeysByProfile[profile] = profileSemanticKey;
					}
					var semanticCacheKey = BuildResolvedMaterialCacheKey(profile, imported, profileSemanticKey);
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

			// Apply per-renderer state (shadow casting, receive shadows, rendering layer mask)
			// that glTF doesn't carry, resolved through node ids.
			var rendererStateStopwatch = Stopwatch.StartNew();
			ApplyRendererStates(payload, tableRoot, resolveNodeById, stats);
			rendererStateStopwatch.Stop();
			stats.RendererStateMilliseconds = rendererStateStopwatch.ElapsedMilliseconds;

			if (stats.UnmatchedNames.Count > 0) {
				var sample = string.Join(", ", TakeFirst(stats.UnmatchedNames, 12));
				Logger.Warn($"vpe material unmatched material-name sample: {sample}");
			}
			Logger.Info(
				$"vpe materials applied from {sourceLabel}: profiles={payload.Profiles.Length}, " +
				$"textures={textureEntries?.Length ?? 0}, " +
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
				$"packedTextureLoads={textures.PackedLoadCount}, " +
				$"resolverStats=[{resolverDiagnostics?.GetDiagnosticsSummary() ?? "n/a"}].");

			return true;
		}

		private static void ApplyRendererStates(
			VpeMaterialsPayload payload,
			Transform tableRoot,
			Func<string, Transform> resolveNodeById,
			Stats stats)
		{
			if (payload.RendererStates == null) {
				return;
			}
			foreach (var state in payload.RendererStates) {
				if (state == null || string.IsNullOrEmpty(state.NodeId)) {
					continue;
				}
				var target = resolveNodeById?.Invoke(state.NodeId);
				if (!target || !target.TryGetComponent<Renderer>(out var renderer)) {
					stats.RendererStatesMissing++;
					continue;
				}
				ApplyRendererState(renderer, state);
				stats.RendererStatesApplied++;
			}
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

		private static Dictionary<string, VpeMaterialProfile> BuildProfileLookup(VpeMaterialProfile[] profiles)
		{
			var lookup = new Dictionary<string, VpeMaterialProfile>(profiles.Length, StringComparer.Ordinal);
			foreach (var profile in profiles) {
				if (profile == null || string.IsNullOrWhiteSpace(profile.Name)) {
					continue;
				}
				lookup[NormalizeMaterialName(profile.Name)] = profile;
			}
			return lookup;
		}

		private static string BuildResolvedMaterialCacheKey(VpeMaterialProfile profile, Material imported, string profileSemanticKey)
		{
			if (profile == null || !imported) {
				return null;
			}

			var texturePropertyNames = imported.GetTexturePropertyNames();
			Array.Sort(texturePropertyNames, StringComparer.Ordinal);

			var builder = new StringBuilder(256);
			builder.Append(profile.Type ?? string.Empty)
				.Append('|')
				.Append(imported.shader ? imported.shader.name : string.Empty)
				.Append(profileSemanticKey);

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

		private static string BuildProfileSemanticKey(VpeMaterialProfile profile)
		{
			var builder = new StringBuilder(256);
			AppendProfileSemanticKey(builder, profile);
			return builder.ToString();
		}

		private static void AppendProfileSemanticKey(StringBuilder builder, VpeMaterialProfile profile)
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

		private static void ApplyRendererState(Renderer renderer, VpeRendererState state)
		{
			renderer.shadowCastingMode = VpeMaterialEnums.ParseShadowCastingMode(state.CastShadows);
			renderer.receiveShadows = state.ReceiveShadows;
			renderer.renderingLayerMask = state.RenderingLayerMask;
			if (state.Hdrp != null && state.Hdrp.RayTracingMode >= 0) {
				renderer.rayTracingMode = (RayTracingMode)state.Hdrp.RayTracingMode;
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
			private readonly Dictionary<string, VpeTexturePayload> _assetsById;
			private readonly byte[] _packedTextureData;
			private readonly Dictionary<string, Texture2D> _loaded = new(StringComparer.Ordinal);
			private readonly HashSet<string> _missingTextureIdsLogged = new(StringComparer.Ordinal);
			private long _loadedBytes;
			private long _loadMilliseconds;
			private int _loadCount;
			private int _cacheHits;
			private int _packedLoadCount;
			public TextureProvider(
				VpeTexturePayload[] assets,
				byte[] packedTextureData)
			{
				_packedTextureData = packedTextureData;
				_assetsById = new Dictionary<string, VpeTexturePayload>(StringComparer.Ordinal);
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
				var loadedFromPacked = false;
				var hasPackedRange = HasPackedTextureRange(asset);
				var isRawPayload = !string.IsNullOrEmpty(asset.PixelFormat);
				// Raw payloads upload straight out of the packed blob (pinned, no per-texture copy);
				// only the encoded-image path still needs its own byte array.
				if (!(isRawPayload && hasPackedRange) && TryReadPackedTextureBytes(asset, out var packedBytes)) {
					bytes = packedBytes;
					loadedFromPacked = true;
				}
				var payloadLength = loadedFromPacked
					? bytes.Length
					: isRawPayload && hasPackedRange ? asset.ByteLength : 0;
				if (payloadLength == 0) {
					var reason = asset.ByteOffset >= 0 && asset.ByteLength > 0
						? $"packed-texture-range-missing:{asset.ByteOffset}+{asset.ByteLength}"
						: $"no-packed-bytes:{asset.Id}";
					LogMissingTexture(textureId, reason: reason);
					_loaded[textureId] = null;
					return null;
				}

				var linear = string.Equals(asset.ColorSpace, VpeColorSpaces.Linear, StringComparison.OrdinalIgnoreCase);
				var generateMipMaps = asset.GenerateMipMaps;
				var loadStopwatch = Stopwatch.StartNew();
				Texture2D texture;
				if (isRawPayload) {
					// Pre-cooked GPU payload: raw bytes in final pixel format with baked mips. Upload
					// directly, no decode, no runtime compression, no mip generation.
					texture = CreateTextureFromPackedRawPayload(asset, linear);
					loadedFromPacked = texture;
					if (!texture) {
						loadStopwatch.Stop();
						LogMissingTexture(textureId, reason: $"raw-payload-failed:{asset.PixelFormat}:{asset.Width}x{asset.Height}");
						_loaded[textureId] = null;
						return null;
					}
				} else {
					texture = new Texture2D(2, 2, TextureFormat.RGBA32, generateMipMaps, linear) {
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
							Logger.Warn(ex, $"vpe materials failed compressing linear texture '{texture.name}'. Keeping uncompressed texture.");
						}
					}
					texture.Apply(updateMipmaps: generateMipMaps, makeNoLongerReadable: true);
				}
				loadStopwatch.Stop();
				texture.wrapMode = (TextureWrapMode)asset.WrapMode;
				texture.filterMode = (FilterMode)asset.FilterMode;
				texture.anisoLevel = Mathf.Max(1, asset.AnisoLevel);
				_loadCount++;
				_loadMilliseconds += loadStopwatch.ElapsedMilliseconds;
				_loadedBytes += payloadLength;
				if (loadedFromPacked) {
					_packedLoadCount++;
				}
				_loaded[textureId] = texture;
				return texture;
			}

			public int LoadCount => _loadCount;
			public int CacheHits => _cacheHits;
			public long LoadMilliseconds => _loadMilliseconds;
			public long LoadedBytes => _loadedBytes;
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
				Logger.Warn($"vpe materials texture lookup failed for TextureId='{textureId}' ({reason}).");
			}

			// Uploads straight out of the packed blob through a pinned pointer, skipping the
			// per-texture byte[] copy (which costs real time across hundreds of megabytes).
			private Texture2D CreateTextureFromPackedRawPayload(VpeTexturePayload asset, bool linear)
			{
				if (!HasPackedTextureRange(asset)) {
					return null;
				}

				var texture = CreateRawPayloadTexture(asset, linear);
				if (!texture) {
					return null;
				}

				var handle = default(System.Runtime.InteropServices.GCHandle);
				try {
					handle = System.Runtime.InteropServices.GCHandle.Alloc(
						_packedTextureData, System.Runtime.InteropServices.GCHandleType.Pinned);
					texture.LoadRawTextureData(
						IntPtr.Add(handle.AddrOfPinnedObject(), asset.ByteOffset), asset.ByteLength);
					texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
					return texture;

				} catch (Exception ex) {
					Logger.Warn(ex, $"vpe materials failed uploading raw texture payload for '{asset.Id}' " +
						$"({asset.PixelFormat}, {asset.Width}x{asset.Height}, mips={asset.MipCount}, bytes={asset.ByteLength}).");
					UnityEngine.Object.Destroy(texture);
					return null;

				} finally {
					if (handle.IsAllocated) {
						handle.Free();
					}
				}
			}

			private static Texture2D CreateRawPayloadTexture(VpeTexturePayload asset, bool linear)
			{
				TextureFormat format;
				switch (asset.PixelFormat) {
					case VpePixelFormats.Bc7:
						format = TextureFormat.BC7;
						break;
					case VpePixelFormats.Dxt5:
						format = TextureFormat.DXT5;
						break;
					case VpePixelFormats.Rgba32:
						format = TextureFormat.RGBA32;
						break;
					default:
						Logger.Warn($"vpe materials unknown raw pixel format '{asset.PixelFormat}' for texture '{asset.Id}'.");
						return null;
				}

				if (asset.Width <= 0 || asset.Height <= 0) {
					return null;
				}
				if (!SystemInfo.SupportsTextureFormat(format)) {
					Logger.Warn($"vpe materials raw pixel format '{asset.PixelFormat}' is not supported on this platform (texture '{asset.Id}').");
					return null;
				}

				var mipCount = Mathf.Max(1, asset.MipCount);
				try {
					return new Texture2D(asset.Width, asset.Height, format, mipCount, linear) {
						name = string.IsNullOrWhiteSpace(asset.SourceName) ? asset.Id : asset.SourceName,
					};
				} catch (Exception ex) {
					Logger.Warn(ex, $"vpe materials failed creating texture for '{asset.Id}' " +
						$"({asset.PixelFormat}, {asset.Width}x{asset.Height}, mips={mipCount}).");
					return null;
				}
			}

			private bool HasPackedTextureRange(VpeTexturePayload asset)
			{
				return asset != null
					&& _packedTextureData != null
					&& asset.ByteOffset >= 0
					&& asset.ByteLength > 0
					&& asset.ByteOffset + (long)asset.ByteLength <= _packedTextureData.Length;
			}

			private bool TryReadPackedTextureBytes(VpeTexturePayload asset, out byte[] bytes)
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
