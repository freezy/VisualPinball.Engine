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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using StbImageSharp;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Applies the ambient occlusion an export baked for the table (<see cref="VpeLitProfile.OcclusionMap"/>).
	/// HDRP and URP Lit read occlusion only from the mask, so the player cooks a texture that carries the authored
	/// mask with the baked occlusion multiplied into its occlusion channel, at the larger of the two resolutions.
	/// A profile without a mask gets the occlusion map itself as its mask, with the metallic and smoothness remaps
	/// pinned to the profile's scalars, so only its occlusion channel counts.
	/// </summary>
	public static class VpeBakedOcclusion
	{
		/// <summary>Separates the mask's and the occlusion's id in the id of the texture made from both.</summary>
		public const string IdSeparator = ":ao:";

		public sealed class Merge
		{
			/// <summary>The texture the profiles reference instead of their mask.</summary>
			public VpeTexture Target;
			/// <summary>The authored mask, or null when the occlusion map is the mask.</summary>
			public VpeTexture Mask;
			/// <summary>The baked occlusion.</summary>
			public VpeTexture Occlusion;
			/// <summary>
			/// Whether the target cooks from the occlusion's bytes, with the mask sampled into it: when there is no
			/// mask, or the occlusion has more texels than the mask. Otherwise it cooks from the mask's bytes.
			/// </summary>
			public bool CooksFromOcclusion;
			/// <summary>The authored mask's occlusion remap, applied before the multiply. The target's is (0, 1).</summary>
			public Vector2 AoRemap = new(0f, 1f);
		}

		/// <summary>
		/// Points every lit profile with baked occlusion at a texture that carries it in the mask's occlusion
		/// channel and drops the occlusion refs. The new textures are added to the payload; the textures that only
		/// the occlusion refs used are left for the caller's pruning. Returns what to cook, keyed by the new ids.
		/// </summary>
		public static Dictionary<string, Merge> Prepare(VpeMaterialsPayload payload)
		{
			var merges = new Dictionary<string, Merge>(StringComparer.Ordinal);
			if (payload?.Profiles == null) {
				return merges;
			}
			var textures = new Dictionary<string, VpeTexture>(StringComparer.Ordinal);
			foreach (var texture in payload.Textures ?? Array.Empty<VpeTexture>()) {
				if (texture != null && !string.IsNullOrEmpty(texture.Id) && !textures.ContainsKey(texture.Id)) {
					textures.Add(texture.Id, texture);
				}
			}
			var added = new List<VpeTexture>();
			foreach (var lit in payload.Profiles.Select(profile => profile?.Lit).Where(lit => lit != null)) {
				var occlusionRef = lit.OcclusionMap;
				lit.OcclusionMap = null;
				if (string.IsNullOrEmpty(occlusionRef?.TextureId) || !textures.TryGetValue(occlusionRef.TextureId, out var occlusion)) {
					continue;
				}
				VpeTexture mask = null;
				if (!string.IsNullOrEmpty(lit.MaskMap?.TextureId)) {
					// The occlusion channel of a glTF-packed mask is red; nothing writes those yet.
					if (lit.MaskPacking != VpeMaskPackings.HdrpMaskMap || !textures.TryGetValue(lit.MaskMap.TextureId, out mask)) {
						continue;
					}
				}
				// The merge puts the occlusion's texels where the mask's are. Both are in the base map's UV space: HDRP
				// and URP Lit sample the mask with the base map's tiling and offset, never with its own.

				// HDRP and URP remap the occlusion channel after sampling it: the merge applies the authored
				// remap to the mask's channel before the multiply, so the remap can't mute the baked occlusion.
				var aoRemap = mask != null ? lit.AoRemap : new Vector2(0f, 1f);
				var id = (mask?.Id ?? string.Empty) + IdSeparator + occlusion.Id;
				if (aoRemap != new Vector2(0f, 1f)) {
					id += FormattableString.Invariant($"{IdSeparator}{aoRemap.x:R},{aoRemap.y:R}");
				}
				if (!merges.ContainsKey(id)) {
					var fromOcclusion = mask == null || (long)occlusion.Width * occlusion.Height > (long)mask.Width * mask.Height;
					// The mask's sampling, the source of whichever file has more texels.
					var target = PackageApi.Packer.Unpack<VpeTexture>(PackageApi.Packer.Pack(mask ?? occlusion));
					if (fromOcclusion) {
						target.FileName = occlusion.FileName;
						target.MimeType = occlusion.MimeType;
						target.Width = occlusion.Width;
						target.Height = occlusion.Height;
					}
					target.Id = id;
					while (textures.ContainsKey(target.Id)) {
						target.Id += IdSeparator;
					}
					target.EditorOnly = false;
					textures.Add(target.Id, target);
					added.Add(target);
					merges.Add(id, new Merge { Target = target, Mask = mask, Occlusion = occlusion, CooksFromOcclusion = fromOcclusion, AoRemap = aoRemap });
				}

				var merged = merges[id].Target;
				if (mask != null) {
					lit.MaskMap = new VpeTextureRef { TextureId = merged.Id, Offset = lit.MaskMap.Offset, Scale = lit.MaskMap.Scale };
					lit.AoRemap = new Vector2(0f, 1f);
				} else {
					// A grayscale map as mask: R=G=B=occlusion, A=1. The pinned remaps keep metallic and
					// smoothness at the scalars whatever the channels hold; no detail map reads blue here.
					lit.MaskMap = new VpeTextureRef { TextureId = merged.Id, Offset = occlusionRef.Offset, Scale = occlusionRef.Scale };
					lit.MaskPacking = VpeMaskPackings.HdrpMaskMap;
					lit.MetallicRemap = new Vector2(lit.Metallic, lit.Metallic);
					lit.SmoothnessRemap = new Vector2(lit.Smoothness, lit.Smoothness);
					lit.AoRemap = new Vector2(0f, 1f);
				}
			}
			if (added.Count > 0) {
				payload.Textures = (payload.Textures ?? Array.Empty<VpeTexture>()).Concat(added).ToArray();
			}
			// Keyed by the target's id, which differs from the merge id only when that id was taken.
			return merges.Values.ToDictionary(merge => merge.Target.Id, StringComparer.Ordinal);
		}

		/// <summary>
		/// Gives the profiles that use a merge's target the authored mask's occlusion remap back, for when the
		/// target holds the authored mask only because the merge couldn't be cooked.
		/// </summary>
		public static void RestoreRemap(VpeMaterialsPayload payload, Merge merge)
		{
			if (merge?.Mask == null || payload?.Profiles == null) {
				return;
			}
			foreach (var lit in payload.Profiles.Select(profile => profile?.Lit).Where(lit => lit?.MaskMap?.TextureId == merge.Target.Id)) {
				lit.AoRemap = merge.AoRemap;
			}
		}

		/// <summary>
		/// Removes the baked occlusion from a payload that is imported for editing: it is the output of an
		/// export, made again by the next one, and the editor's materials keep their authored masks.
		/// </summary>
		public static void Strip(VpeMaterialsPayload payload)
		{
			if (payload?.Profiles == null) {
				return;
			}
			var occlusionIds = new HashSet<string>(StringComparer.Ordinal);
			foreach (var lit in payload.Profiles.Select(profile => profile?.Lit).Where(lit => lit != null)) {
				if (!string.IsNullOrEmpty(lit.OcclusionMap?.TextureId)) {
					occlusionIds.Add(lit.OcclusionMap.TextureId);
				}
				lit.OcclusionMap = null;
			}
			if (occlusionIds.Count == 0 || payload.Textures == null) {
				return;
			}
			var used = ReferencedTextureIds(payload);
			payload.Textures = payload.Textures.Where(t => t == null || !occlusionIds.Contains(t.Id) || used.Contains(t.Id)).ToArray();
		}

		/// <summary>
		/// Decodes an occlusion map into one byte per texel, bottom row first (the row order of the cook).
		/// </summary>
		public static byte[] DecodeOcclusion(byte[] encoded, out int width, out int height)
			=> Decode(encoded, ColorComponents.Grey, 1, out width, out height);

		/// <summary>Decodes a mask into RGBA32 texels, bottom row first (the row order of the cook).</summary>
		public static byte[] DecodeMask(byte[] encoded, out int width, out int height)
			=> Decode(encoded, ColorComponents.RedGreenBlueAlpha, 4, out width, out height);

		private static byte[] Decode(byte[] encoded, ColorComponents components, int channels, out int width, out int height)
		{
			var image = ImageResult.FromMemory(encoded, components);
			if (image?.Data == null || image.Width <= 0 || image.Height <= 0) {
				throw new InvalidDataException("Cannot decode a texture of the baked occlusion.");
			}
			width = image.Width;
			height = image.Height;
			var pixels = image.Data;
			var stride = width * channels;
			var row = new byte[stride];
			for (var y = 0; y < height / 2; y++) {
				var top = y * stride;
				var bottom = (height - 1 - y) * stride;
				Buffer.BlockCopy(pixels, top, row, 0, stride);
				Buffer.BlockCopy(pixels, bottom, pixels, top, stride);
				Buffer.BlockCopy(row, 0, pixels, bottom, stride);
			}
			return pixels;
		}

		/// <summary>
		/// Multiplies the occlusion into the green channel of RGBA32 mask pixels (bottom row first), after applying
		/// the authored occlusion remap to it. The two maps cover the same UV square and may differ in size: the
		/// occlusion is sampled bilinearly at each mask texel.
		/// </summary>
		public static void MultiplyInto(byte[] rgba, int width, int height, byte[] occlusion, int occlusionWidth, int occlusionHeight, Vector2 aoRemap)
		{
			if (rgba == null || occlusion == null || rgba.Length < (long)width * height * 4 || occlusion.Length < (long)occlusionWidth * occlusionHeight) {
				throw new ArgumentException("Pixel buffers don't match their sizes.");
			}
			Parallel.For(0, height, y => {
				for (var x = 0; x < width; x++) {
					var ao = Sample(occlusion, occlusionWidth, occlusionHeight, 1, 0, x, y, width, height);
					var i = ((long)y * width + x) * 4 + 1;
					rgba[i] = Compose(rgba[i], ao, aoRemap);
				}
			});
		}

		/// <summary>
		/// Turns the RGBA32 pixels of a decoded occlusion map (bottom row first, occlusion in red) into the mask
		/// with the occlusion multiplied into its green channel, after applying the authored occlusion remap to it.
		/// The mask covers the same UV square and is sampled at each occlusion texel the way the GPU would sample
		/// it: wrapped when it repeats (else clamped), and from the nearest texel when it is point filtered (else
		/// bilinearly).
		/// </summary>
		public static void ComposeInto(byte[] rgba, int width, int height, byte[] mask, int maskWidth, int maskHeight, Vector2 aoRemap,
			bool maskRepeats = false, bool maskPointFiltered = false)
		{
			if (rgba == null || mask == null || rgba.Length < (long)width * height * 4 || mask.Length < (long)maskWidth * maskHeight * 4) {
				throw new ArgumentException("Pixel buffers don't match their sizes.");
			}
			Parallel.For(0, height, y => {
				for (var x = 0; x < width; x++) {
					var i = ((long)y * width + x) * 4;
					float ao = rgba[i];
					rgba[i] = ToByte(Sample(mask, maskWidth, maskHeight, 4, 0, x, y, width, height, maskRepeats, maskPointFiltered));
					rgba[i + 1] = Compose(ToByte(Sample(mask, maskWidth, maskHeight, 4, 1, x, y, width, height, maskRepeats, maskPointFiltered)), ao, aoRemap);
					rgba[i + 2] = ToByte(Sample(mask, maskWidth, maskHeight, 4, 2, x, y, width, height, maskRepeats, maskPointFiltered));
					rgba[i + 3] = ToByte(Sample(mask, maskWidth, maskHeight, 4, 3, x, y, width, height, maskRepeats, maskPointFiltered));
				}
			});
		}

		/// <summary>
		/// RGBA32 pixels scaled down to at most the given size, each texel the area-weighted average of the ones it
		/// covers: what the cook makes of a source file larger than its texture's authored size. The pixels
		/// themselves when they are no larger.
		/// </summary>
		public static byte[] Downsample(byte[] rgba, int width, int height, int maxWidth, int maxHeight, out int newWidth, out int newHeight)
		{
			newWidth = maxWidth > 0 ? System.Math.Min(width, maxWidth) : width;
			newHeight = maxHeight > 0 ? System.Math.Min(height, maxHeight) : height;
			if (newWidth == width && newHeight == height) {
				return rgba;
			}
			if (rgba == null || rgba.Length < (long)width * height * 4) {
				throw new ArgumentException("Pixel buffer doesn't match its size.");
			}
			var tw = newWidth;
			var th = newHeight;
			var columns = Footprints(width, tw);
			var rows = Footprints(height, th);
			var result = new byte[(long)tw * th * 4];
			Parallel.For(0, th, y => {
				var sum = new double[4];
				for (var x = 0; x < tw; x++) {
					System.Array.Clear(sum, 0, 4);
					foreach (var (sy, wy) in rows[y]) {
						foreach (var (sx, wx) in columns[x]) {
							var w = wy * wx;
							var i = ((long)sy * width + sx) * 4;
							sum[0] += rgba[i] * w; sum[1] += rgba[i + 1] * w; sum[2] += rgba[i + 2] * w; sum[3] += rgba[i + 3] * w;
						}
					}
					var o = ((long)y * tw + x) * 4;
					for (var c = 0; c < 4; c++) {
						result[o + c] = (byte)System.Math.Clamp((int)System.Math.Round(sum[c], MidpointRounding.AwayFromZero), 0, 255);
					}
				}
			});
			return result;
		}

		// For each texel of a row of the given size, the source texels it covers and how much of it each covers
		// (the weights add up to one).
		private static (int Index, double Weight)[][] Footprints(int source, int target)
		{
			var footprints = new (int, double)[target][];
			var step = (double)source / target;
			for (var t = 0; t < target; t++) {
				var from = t * step;
				var to = (t + 1) * step;
				var texels = new List<(int, double)>();
				for (var s = (int)System.Math.Floor(from); s < System.Math.Min(source, (int)System.Math.Ceiling(to)); s++) {
					var covered = System.Math.Min(to, s + 1) - System.Math.Max(from, s);
					if (covered > 0) {
						texels.Add((s, covered / step));
					}
				}
				footprints[t] = texels.ToArray();
			}
			return footprints;
		}

		private static byte ToByte(float value) => (byte)Mathf.Clamp(value + 0.5f, 0f, 255f);

		// The remapped authored occlusion times the baked one (0..255).
		private static byte Compose(byte authored, float baked, Vector2 aoRemap)
		{
			var remapped = Mathf.Clamp01(aoRemap.x + (aoRemap.y - aoRemap.x) * (authored / 255f));
			return ToByte(remapped * baked);
		}

		// One channel of a map sampled at the center of texel (x, y) of a map of the given size over the same UV
		// square: bilinearly or from the nearest texel, its neighbors clamped to the edge or wrapped around.
		private static float Sample(byte[] map, int mapWidth, int mapHeight, int channels, int channel, int x, int y, int width, int height,
			bool repeat = false, bool point = false)
		{
			var cx = (x + 0.5f) * mapWidth / width;
			var cy = (y + 0.5f) * mapHeight / height;
			if (point) {
				var px = System.Math.Min((int)cx, mapWidth - 1);
				var py = System.Math.Min((int)cy, mapHeight - 1);
				return map[((long)py * mapWidth + px) * channels + channel];
			}
			var sx = cx - 0.5f;
			var sy = cy - 0.5f;
			var x0 = (int)Mathf.Floor(sx);
			var y0 = (int)Mathf.Floor(sy);
			var fx = sx - x0;
			var fy = sy - y0;
			int Wrap(int value, int size) => repeat ? ((value % size) + size) % size : System.Math.Clamp(value, 0, size - 1);
			var x1 = Wrap(x0 + 1, mapWidth);
			var y1 = Wrap(y0 + 1, mapHeight);
			x0 = Wrap(x0, mapWidth);
			y0 = Wrap(y0, mapHeight);
			float At(int px, int py) => map[((long)py * mapWidth + px) * channels + channel];
			var top = At(x0, y0) + (At(x1, y0) - At(x0, y0)) * fx;
			var bottom = At(x0, y1) + (At(x1, y1) - At(x0, y1)) * fx;
			return top + (bottom - top) * fy;
		}

		private static HashSet<string> ReferencedTextureIds(VpeMaterialsPayload payload)
		{
			var json = System.Text.Encoding.UTF8.GetString(PackageApi.Packer.Pack(payload.Profiles ?? Array.Empty<VpeMaterialProfile>()));
			return new HashSet<string>(((JContainer)JToken.Parse(json)).DescendantsAndSelf().OfType<JProperty>()
				.Where(p => p.Name == "textureId" && p.Value.Type == JTokenType.String).Select(p => (string)p.Value), StringComparer.Ordinal);
		}
	}
}
