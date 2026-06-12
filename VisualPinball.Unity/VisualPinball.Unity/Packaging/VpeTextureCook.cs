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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StbImageSharp;
using UnityEngine;
using UnityEngine.Rendering;
using Logger = NLog.Logger;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Cooks the lossless source textures of a .vpe package into GPU-ready payloads: decodes
	/// PNG/JPEG on worker threads (StbImageSharp), re-packs normals into HDRP's AG layout, bakes
	/// mip chains, block-compresses to BC7 on the GPU (see VpeBc7GpuEncoder) and assembles the
	/// blob + manifest that VpeTextureCache persists. A cache hit on a later load skips all of
	/// this and uploads the cooked bytes directly.
	/// </summary>
	public static class VpeTextureCook
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private const string RawMimeType = "application/x-vpe-raw";
		// Pixels of BC7 encode work dispatched per frame. Keeps single frames well below the
		// Windows GPU watchdog threshold (the same lesson as the upload budget in the reader).
		private const long EncodePixelYieldBudget = 32L * 1024 * 1024;
		// Bounded hand-off between decode workers and the GPU stage; also bounds peak memory of
		// decoded-but-not-yet-encoded RGBA data. Generous on purpose: while the main thread is busy
		// decoding the big files, the workers must be able to keep going without backpressure.
		private const int GpuQueueCapacity = 96;

		public sealed class Result
		{
			public VpeTextureCache.Manifest Manifest;
			public byte[] CookedData;
		}

		private sealed class CookPlanItem
		{
			public VpeTexturePayload Source;
			public VpeTexturePayload Cooked;
			public bool Passthrough;
			public bool IsNormal;
			public bool UseBc7;
			public bool MainThreadDecode;
			public int[] MipWidths;
			public int[] MipHeights;
			public long[] MipOffsets; // absolute offsets into the cooked blob
		}

		// Source files at least this big decode on the main thread with Unity's native LoadImage.
		// PNG decoding is single-threaded per image, so a handful of huge playfield/cabinet maps
		// would otherwise dominate the whole cook wall time on the (slower) managed decoder.
		private const int MainThreadDecodeThresholdBytes = 6 * 1024 * 1024;

		private sealed class GpuWork
		{
			public CookPlanItem Item;
			public byte[] BasePixels; // RGBA32 base level only; mips are generated on the GPU
			public long TotalPixels;
		}

		public static bool IsSupported => VpeBc7GpuEncoder.IsSupported;

		/// <summary>
		/// Cooks the given source textures. Returns null when cooking is not possible (no compute
		/// support, decode failures); callers then fall back to the slow decode-at-load path.
		/// Must be awaited on the main thread.
		/// </summary>
		public static async Task<Result> CookAsync(
			VpeTexturePayload[] textures,
			HashSet<string> normalIds,
			byte[] sourceTextureData,
			Action<int, int> reportProgress,
			CancellationToken cancellationToken)
		{
			if (textures == null || textures.Length == 0 || sourceTextureData == null) {
				return null;
			}

			using var encoder = new VpeBc7GpuEncoder();
			if (!encoder.Initialize()) {
				Logger.Warn("VpeTextureCook: GPU BC7 encoder unavailable; textures will decode at load time.");
				return null;
			}

			var cookStopwatch = Stopwatch.StartNew();
			long decodeWallMs = 0;
			long decodeCpuMs = 0;
			long uploadMs = 0;
			long firstGpuItemMs = 0;
			normalIds ??= new HashSet<string>(StringComparer.Ordinal);

			// Plan pass: image headers only. Determines final dimensions, formats, mip counts and
			// blob offsets up front so workers can write into disjoint ranges without coordination.
			var plan = BuildPlan(textures, sourceTextureData, normalIds, out var cookedSize);
			if (plan.Count == 0) {
				return null;
			}
			var cookedData = new byte[cookedSize];

			var totalItems = plan.Count;
			var finishedItems = 0;
			var decodeFailures = 0;
			var gpuFailures = 0;

			// The few huge source files decode on the main thread via Unity's native LoadImage;
			// everything else goes to the managed worker pool. PNG decode is single-threaded per
			// image, so without this split one 50 MB cabinet normal dominates the whole cook.
			var mainThreadItems = new Queue<CookPlanItem>();
			var workerItems = new List<CookPlanItem>(plan.Count);
			foreach (var planned in plan) {
				if (planned.MainThreadDecode) {
					mainThreadItems.Enqueue(planned);
				} else {
					workerItems.Add(planned);
				}
			}

			// Decode stage: parallel workers stream BC7 work to the main thread; RGBA32 and
			// passthrough items are written straight into the blob from the worker.
			var gpuQueue = new BlockingCollection<GpuWork>(GpuQueueCapacity);
			var decodeTask = Task.Run(() => {
				try {
					var options = new ParallelOptions {
						// The cook runs behind a loading screen; decoding is the long pole, so use
						// nearly every core.
						MaxDegreeOfParallelism = Mathf.Clamp(Environment.ProcessorCount - 1, 2, 16),
						CancellationToken = cancellationToken,
					};
					Parallel.ForEach(workerItems, options, item => {
						try {
							if (item.Passthrough) {
								Buffer.BlockCopy(sourceTextureData, item.Source.ByteOffset, cookedData, (int)item.Cooked.ByteOffset, item.Source.ByteLength);
								Interlocked.Increment(ref finishedItems);
								return;
							}

							var decodeStopwatch = Stopwatch.StartNew();
							var work = DecodeAndPrepare(item, sourceTextureData);
							Interlocked.Add(ref decodeCpuMs, decodeStopwatch.ElapsedMilliseconds);
							if (work == null) {
								Interlocked.Increment(ref decodeFailures);
								return;
							}

							if (item.UseBc7) {
								gpuQueue.Add(work, cancellationToken);
							} else {
								// Raw RGBA32: build the mip chain on the CPU (rare path, small
								// non-block-compressible textures) and write the final payload.
								WriteRgba32MipChain(item, work.BasePixels, cookedData);
								Interlocked.Increment(ref finishedItems);
							}
						} catch (OperationCanceledException) {
							throw;
						} catch (Exception ex) {
							Logger.Warn(ex, $"VpeTextureCook: failed decoding '{item.Source.Id}'.");
							Interlocked.Increment(ref decodeFailures);
						}
					});
				} finally {
					Interlocked.Exchange(ref decodeWallMs, cookStopwatch.ElapsedMilliseconds);
					gpuQueue.CompleteAdding();
				}
			}, cancellationToken);

			// GPU stage (main thread): upload base levels, build mips on the GPU, dispatch BC7
			// encoding and async-read the blocks back into the blob. Yields by dispatched-pixel
			// budget to keep frames sane.
			var outstandingTextures = 0;
			long pixelsSinceYield = 0;
			var normalRepackShader = Resources.Load<Shader>("VpeCookNormalRepack");
			var normalRepackMaterial = normalRepackShader
				? new Material(normalRepackShader) { hideFlags = HideFlags.HideAndDontSave }
				: null;

			void SubmitToGpu(CookPlanItem item, Texture2D upload, bool repackNormalOnGpu)
			{
				if (firstGpuItemMs == 0) {
					firstGpuItemMs = cookStopwatch.ElapsedMilliseconds;
				}
				RenderTexture mipped = null;
				try {
					mipped = new RenderTexture(item.Cooked.Width, item.Cooked.Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) {
						name = $"{item.Source.Id} (CookMips)",
						useMipMap = item.Cooked.MipCount > 1,
						autoGenerateMips = false,
						hideFlags = HideFlags.HideAndDontSave,
					};
					mipped.Create();
					if (repackNormalOnGpu && normalRepackMaterial) {
						Graphics.Blit(upload, mipped, normalRepackMaterial);
					} else {
						Graphics.Blit(upload, mipped);
					}
					if (item.Cooked.MipCount > 1) {
						mipped.GenerateMips();
					}
				} catch (Exception ex) {
					Logger.Warn(ex, $"VpeTextureCook: failed preparing '{item.Source.Id}' for encoding.");
					DestroyObject(upload);
					if (mipped) {
						mipped.Release();
						DestroyObject(mipped);
					}
					gpuFailures++;
					return;
				}

				var baseTexture = upload;
				var mipSource = mipped;
				var remainingMips = item.Cooked.MipCount;
				Interlocked.Increment(ref outstandingTextures);
				for (var mip = 0; mip < item.Cooked.MipCount; mip++) {
					encoder.EncodeMip(mipSource, mip, item.MipWidths[mip], item.MipHeights[mip], cookedData, (int)item.MipOffsets[mip], ok => {
						if (!ok) {
							gpuFailures++;
						}
						if (--remainingMips == 0) {
							DestroyObject(baseTexture);
							mipSource.Release();
							DestroyObject(mipSource);
							Interlocked.Decrement(ref outstandingTextures);
							Interlocked.Increment(ref finishedItems);
						}
					});
				}

				// rough: base level plus one third for the mip chain
				pixelsSinceYield += (long)item.Cooked.Width * item.Cooked.Height * 4 / 3;
				reportProgress?.Invoke(Volatile.Read(ref finishedItems), totalItems);
			}

			Texture2D PrepareWorkerUpload(GpuWork work)
			{
				var uploadStopwatch = Stopwatch.StartNew();
				Texture2D upload = null;
				try {
					upload = new Texture2D(work.Item.Cooked.Width, work.Item.Cooked.Height, TextureFormat.RGBA32, false, linear: true) {
						name = $"{work.Item.Source.Id} (CookSource)",
						hideFlags = HideFlags.HideAndDontSave,
					};
					upload.LoadRawTextureData(work.BasePixels);
					upload.Apply(updateMipmaps: false, makeNoLongerReadable: false);
					uploadMs += uploadStopwatch.ElapsedMilliseconds;
					return upload;
				} catch (Exception ex) {
					Logger.Warn(ex, $"VpeTextureCook: failed uploading '{work.Item.Source.Id}' for encoding.");
					if (upload) {
						DestroyObject(upload);
					}
					gpuFailures++;
					return null;
				}
			}

			Texture2D DecodeOnMainThread(CookPlanItem item)
			{
				var decodeStopwatch = Stopwatch.StartNew();
				Texture2D decoded = null;
				try {
					var bytes = new byte[item.Source.ByteLength];
					Buffer.BlockCopy(sourceTextureData, item.Source.ByteOffset, bytes, 0, bytes.Length);
					decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear: true) {
						name = $"{item.Source.Id} (CookSource)",
						hideFlags = HideFlags.HideAndDontSave,
					};
					if (!ImageConversion.LoadImage(decoded, bytes, markNonReadable: false)) {
						DestroyObject(decoded);
						decodeFailures++;
						return null;
					}

					if (item.IsNormal && !normalRepackMaterial) {
						// CPU fallback when the repack shader is unavailable; normally the AG
						// repack happens on the GPU during the cook blit.
						var pixels = decoded.GetPixels32();
						for (var i = 0; i < pixels.Length; i++) {
							var p = pixels[i];
							var x = (byte)((p.r * p.a + 127) / 255);
							pixels[i] = new Color32(255, p.g, 255, x);
						}
						var repacked = new Texture2D(decoded.width, decoded.height, TextureFormat.RGBA32, false, linear: true) {
							name = decoded.name,
							hideFlags = HideFlags.HideAndDontSave,
						};
						repacked.SetPixels32(pixels);
						repacked.Apply(updateMipmaps: false, makeNoLongerReadable: false);
						DestroyObject(decoded);
						decoded = repacked;
					}

					Interlocked.Add(ref decodeCpuMs, decodeStopwatch.ElapsedMilliseconds);
					return decoded;
				} catch (Exception ex) {
					Logger.Warn(ex, $"VpeTextureCook: failed main-thread decode of '{item.Source.Id}'.");
					if (decoded) {
						DestroyObject(decoded);
					}
					decodeFailures++;
					return null;
				}
			}

			while (true) {
				cancellationToken.ThrowIfCancellationRequested();

				// The big files are the long pole: decode them first while the workers fill the
				// queue, then drain whatever the workers produced.
				if (mainThreadItems.Count > 0) {
					var item = mainThreadItems.Dequeue();
					var upload = DecodeOnMainThread(item);
					if (upload) {
						SubmitToGpu(item, upload, repackNormalOnGpu: item.IsNormal);
					}
					// One big decode blocks long enough; give the player loop a frame either way.
					await Task.Yield();
					continue;
				}

				GpuWork work;
				try {
					if (!gpuQueue.TryTake(out work, 10, cancellationToken)) {
						if (gpuQueue.IsCompleted) {
							break;
						}
						await Task.Yield();
						continue;
					}
				} catch (InvalidOperationException) {
					break; // completed between checks
				}

				var workerUpload = PrepareWorkerUpload(work);
				if (workerUpload) {
					// Worker-decoded normals arrive already AG-packed.
					SubmitToGpu(work.Item, workerUpload, repackNormalOnGpu: false);
				}

				if (pixelsSinceYield > EncodePixelYieldBudget) {
					pixelsSinceYield = 0;
					await Task.Yield();
				}
			}

			await decodeTask;

			// Flush all pending readbacks; their callbacks run inside WaitAllRequests.
			AsyncGPUReadback.WaitAllRequests();
			var guard = 0;
			while (Volatile.Read(ref outstandingTextures) > 0 && guard++ < 1000) {
				await Task.Yield();
				AsyncGPUReadback.WaitAllRequests();
			}
			DestroyObject(normalRepackMaterial);

			if (decodeFailures > 0 || gpuFailures > 0 || Volatile.Read(ref outstandingTextures) > 0) {
				Logger.Warn($"VpeTextureCook: cook aborted (decodeFailures={decodeFailures}, gpuFailures={gpuFailures}).");
				return null;
			}

			cookStopwatch.Stop();
			var manifest = new VpeTextureCache.Manifest {
				Textures = BuildManifestTextures(textures, plan),
				AgPackedNormalIds = CollectCookedNormalIds(plan),
			};
			Logger.Info(
				$"VpeTextureCook: cooked {plan.Count} texture(s), {cookedSize / 1024f / 1024f:F1} MB " +
				$"in {cookStopwatch.ElapsedMilliseconds}ms " +
				$"(decodeWall={Interlocked.Read(ref decodeWallMs)}ms, decodeCpuSum={Interlocked.Read(ref decodeCpuMs)}ms, " +
				$"firstGpuItem={firstGpuItemMs}ms, mainThreadUploads={uploadMs}ms).");
			return new Result { Manifest = manifest, CookedData = cookedData };
		}

		/// <summary>
		/// Swaps the internal texture entries for their cooked counterparts (GPU-ready payloads
		/// pointing into the cooked blob).
		/// </summary>
		public static VpeTexturePayload[] ReplaceEntries(VpeTexturePayload[] entries, VpeTextureCache.Manifest manifest)
		{
			if (entries == null || manifest?.Textures == null) {
				return entries;
			}

			var cookedById = new Dictionary<string, VpeTexturePayload>(StringComparer.Ordinal);
			foreach (var cooked in manifest.Textures) {
				if (cooked != null && !string.IsNullOrEmpty(cooked.Id)) {
					cookedById[cooked.Id] = cooked;
				}
			}

			var result = (VpeTexturePayload[])entries.Clone();
			for (var i = 0; i < result.Length; i++) {
				var asset = result[i];
				if (asset != null && !string.IsNullOrEmpty(asset.Id) && cookedById.TryGetValue(asset.Id, out var cooked)) {
					result[i] = cooked;
				}
			}
			return result;
		}

		/// <summary>
		/// Flips normal-map refs whose cooked payload is AG-packed to dxt5nm packing, so the
		/// resolver uses them as-is instead of re-packing.
		/// </summary>
		public static void RewriteNormalRefs(VpeMaterialsPayload payload, VpeTextureCache.Manifest manifest)
		{
			var agPacked = new HashSet<string>(manifest?.AgPackedNormalIds ?? Array.Empty<string>(), StringComparer.Ordinal);
			if (agPacked.Count == 0 || payload?.Profiles == null) {
				return;
			}
			foreach (var profile in payload.Profiles) {
				RewriteNormalRef(profile?.Lit?.NormalMap, agPacked);
				RewriteNormalRef(profile?.Decal?.NormalMap, agPacked);
			}
		}

		private static void RewriteNormalRef(VpeNormalMapRef normalMap, HashSet<string> agPackedIds)
		{
			if (normalMap != null && !string.IsNullOrEmpty(normalMap.TextureId) && agPackedIds.Contains(normalMap.TextureId)) {
				normalMap.Packing = VpeNormalPackings.Dxt5nm;
				normalMap.RuntimeCompress = false;
			}
		}

		/// <summary>
		/// Ids of all textures referenced as normal maps by the payload's profiles. Used by the
		/// cook (AG repack) and by the editor importer (normal-map import settings).
		/// </summary>
		public static HashSet<string> CollectNormalTextureIds(VpeMaterialsPayload payload)
		{
			var ids = new HashSet<string>(StringComparer.Ordinal);
			if (payload?.Profiles == null) {
				return ids;
			}
			foreach (var profile in payload.Profiles) {
				var litNormal = profile?.Lit?.NormalMap?.TextureId;
				if (!string.IsNullOrEmpty(litNormal)) {
					ids.Add(litNormal);
				}
				var decalNormal = profile?.Decal?.NormalMap?.TextureId;
				if (!string.IsNullOrEmpty(decalNormal)) {
					ids.Add(decalNormal);
				}
			}
			return ids;
		}

		private static List<CookPlanItem> BuildPlan(
			VpeTexturePayload[] assets,
			byte[] sourceData,
			HashSet<string> normalIds,
			out long cookedSize)
		{
			var plan = new List<CookPlanItem>(assets.Length);
			cookedSize = 0;

			foreach (var asset in assets) {
				if (asset == null || string.IsNullOrEmpty(asset.Id)
					|| asset.ByteOffset < 0 || asset.ByteLength <= 0
					|| asset.ByteOffset + (long)asset.ByteLength > sourceData.Length) {
					continue;
				}

				var cooked = CloneAsset(asset);
				var item = new CookPlanItem { Source = asset, Cooked = cooked };

				var alreadyRaw = !string.IsNullOrEmpty(asset.PixelFormat);
				var isImage = string.Equals(asset.MimeType, "image/png", StringComparison.OrdinalIgnoreCase)
					|| string.Equals(asset.MimeType, "image/jpeg", StringComparison.OrdinalIgnoreCase);

				ImageInfo? header = null;
				if (!alreadyRaw && isImage) {
					try {
						using var stream = new MemoryStream(sourceData, asset.ByteOffset, asset.ByteLength, false);
						header = ImageInfo.FromStream(stream);
					} catch {
						header = null;
					}
				}

				if (alreadyRaw || header == null || header.Value.Width <= 0 || header.Value.Height <= 0) {
					// Undecodable entries travel into the cache verbatim;
					// the reader handles them exactly as it would from the package.
					item.Passthrough = true;
					cooked.ByteOffset = checked((int)cookedSize);
					cooked.ByteLength = asset.ByteLength;
					cookedSize += asset.ByteLength;
					plan.Add(item);
					continue;
				}

				// Authored intent: the imported (possibly importer-clamped) size from the package,
				// further reduced by the local resolution divisor.
				var width = header.Value.Width;
				var height = header.Value.Height;
				var maxWidth = asset.Width > 0 ? asset.Width : width;
				var maxHeight = asset.Height > 0 ? asset.Height : height;
				while (width > maxWidth || height > maxHeight) {
					width = Mathf.Max(1, width >> 1);
					height = Mathf.Max(1, height >> 1);
				}
				for (var d = VpeTextureCookSettings.ResolutionDivisor; d > 1; d >>= 1) {
					width = Mathf.Max(1, width >> 1);
					height = Mathf.Max(1, height >> 1);
				}

				var mipCount = asset.GenerateMipMaps ? MipCountFor(width, height) : 1;
				item.IsNormal = normalIds.Contains(asset.Id);
				item.UseBc7 = VpeTextureCookSettings.CompressTextures
					&& width % 4 == 0 && height % 4 == 0 && width >= 4 && height >= 4;
				item.MainThreadDecode = item.UseBc7 && asset.ByteLength >= MainThreadDecodeThresholdBytes;
				item.MipWidths = new int[mipCount];
				item.MipHeights = new int[mipCount];
				item.MipOffsets = new long[mipCount];

				cooked.Width = width;
				cooked.Height = height;
				cooked.MipCount = mipCount;
				cooked.PixelFormat = item.UseBc7 ? VpePixelFormats.Bc7 : VpePixelFormats.Rgba32;
				cooked.MimeType = RawMimeType;
				cooked.RuntimeCompress = false;
				cooked.ByteOffset = checked((int)cookedSize);

				long itemSize = 0;
				var mw = width;
				var mh = height;
				for (var m = 0; m < mipCount; m++) {
					item.MipWidths[m] = mw;
					item.MipHeights[m] = mh;
					item.MipOffsets[m] = cookedSize + itemSize;
					itemSize += item.UseBc7
						? (long)((mw + 3) / 4) * ((mh + 3) / 4) * 16
						: (long)mw * mh * 4;
					mw = Mathf.Max(1, mw >> 1);
					mh = Mathf.Max(1, mh >> 1);
				}
				cooked.ByteLength = checked((int)itemSize);
				cookedSize += itemSize;
				plan.Add(item);
			}

			if (cookedSize > int.MaxValue) {
				Logger.Warn($"VpeTextureCook: cooked payload would exceed 2 GB ({cookedSize} bytes); not cooking.");
				plan.Clear();
				cookedSize = 0;
			}
			return plan;
		}

		// Decodes the source image, flips it into Unity's bottom-up row order, AG-packs normals and
		// resizes to the planned base dimensions. Mip generation happens later on the GPU. Runs on
		// worker threads — no Unity API access here.
		private static GpuWork DecodeAndPrepare(CookPlanItem item, byte[] sourceData)
		{
			ImageResult image;
			using (var stream = new MemoryStream(sourceData, item.Source.ByteOffset, item.Source.ByteLength, false)) {
				image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
			}
			if (image?.Data == null || image.Width <= 0 || image.Height <= 0) {
				return null;
			}

			var pixels = image.Data;
			var width = image.Width;
			var height = image.Height;
			FlipVertical(pixels, width, height);

			if (item.IsNormal) {
				// x = r * a covers plain RGB sources (a=1 → x=r) and pre-swizzled data alike;
				// output layout (1, y, 1, x) is what HDRP's RGorAG unpack expects.
				for (var i = 0; i < pixels.Length; i += 4) {
					var x = (byte)((pixels[i] * pixels[i + 3] + 127) / 255);
					pixels[i] = 255;
					pixels[i + 2] = 255;
					pixels[i + 3] = x;
				}
			}

			while (width > item.Cooked.Width || height > item.Cooked.Height) {
				pixels = HalveBox(pixels, width, height, out width, out height);
			}
			if (width != item.Cooked.Width || height != item.Cooked.Height) {
				Logger.Warn(
					$"VpeTextureCook: '{item.Source.Id}' decoded to {width}x{height}, expected " +
					$"{item.Cooked.Width}x{item.Cooked.Height}.");
				return null;
			}

			return new GpuWork { Item = item, BasePixels = pixels, TotalPixels = (long)width * height * 4 / 3 };
		}

		// CPU fallback for textures that can't be block-compressed: writes the full RGBA32 mip
		// chain straight into the cooked blob at the planned offsets.
		private static void WriteRgba32MipChain(CookPlanItem item, byte[] basePixels, byte[] cookedData)
		{
			Buffer.BlockCopy(basePixels, 0, cookedData, (int)item.MipOffsets[0], basePixels.Length);
			var current = basePixels;
			var width = item.MipWidths[0];
			var height = item.MipHeights[0];
			for (var m = 1; m < item.Cooked.MipCount; m++) {
				current = HalveBox(current, width, height, out width, out height);
				Buffer.BlockCopy(current, 0, cookedData, (int)item.MipOffsets[m], current.Length);
			}
		}

		private static void FlipVertical(byte[] rgba, int width, int height)
		{
			var stride = width * 4;
			var row = new byte[stride];
			for (var y = 0; y < height / 2; y++) {
				var top = y * stride;
				var bottom = (height - 1 - y) * stride;
				Buffer.BlockCopy(rgba, top, row, 0, stride);
				Buffer.BlockCopy(rgba, bottom, rgba, top, stride);
				Buffer.BlockCopy(row, 0, rgba, bottom, stride);
			}
		}

		// 2x2 box downsample with edge clamping, averaging raw bytes — the same gamma-space box
		// filter Unity's CPU mip generation applies, so cooked mips match the legacy pipeline.
		private static byte[] HalveBox(byte[] src, int width, int height, out int outWidth, out int outHeight)
		{
			outWidth = Mathf.Max(1, width >> 1);
			outHeight = Mathf.Max(1, height >> 1);
			var dst = new byte[outWidth * outHeight * 4];
			for (var y = 0; y < outHeight; y++) {
				var y0 = Mathf.Min(y * 2, height - 1);
				var y1 = Mathf.Min(y * 2 + 1, height - 1);
				for (var x = 0; x < outWidth; x++) {
					var x0 = Mathf.Min(x * 2, width - 1);
					var x1 = Mathf.Min(x * 2 + 1, width - 1);
					var i00 = (y0 * width + x0) * 4;
					var i01 = (y0 * width + x1) * 4;
					var i10 = (y1 * width + x0) * 4;
					var i11 = (y1 * width + x1) * 4;
					var o = (y * outWidth + x) * 4;
					for (var c = 0; c < 4; c++) {
						dst[o + c] = (byte)((src[i00 + c] + src[i01 + c] + src[i10 + c] + src[i11 + c] + 2) >> 2);
					}
				}
			}
			return dst;
		}

		private static void DestroyObject(Object obj)
		{
			if (!obj) {
				return;
			}
			if (Application.isPlaying) {
				Object.Destroy(obj);
			} else {
				Object.DestroyImmediate(obj);
			}
		}

		private static int MipCountFor(int width, int height)
		{
			var count = 1;
			var size = Mathf.Max(width, height);
			while (size > 1) {
				size >>= 1;
				count++;
			}
			return count;
		}

		private static VpeTexturePayload CloneAsset(VpeTexturePayload asset)
		{
			return PackageApi.Packer.Unpack<VpeTexturePayload>(PackageApi.Packer.Pack(asset));
		}

		private static VpeTexturePayload[] BuildManifestTextures(VpeTexturePayload[] assets, List<CookPlanItem> plan)
		{
			var byId = new Dictionary<string, VpeTexturePayload>(StringComparer.Ordinal);
			foreach (var item in plan) {
				byId[item.Source.Id] = item.Cooked;
			}

			var result = new List<VpeTexturePayload>(plan.Count);
			foreach (var asset in assets) {
				if (asset != null && !string.IsNullOrEmpty(asset.Id) && byId.TryGetValue(asset.Id, out var cooked)) {
					result.Add(cooked);
				}
			}
			return result.ToArray();
		}

		private static string[] CollectCookedNormalIds(List<CookPlanItem> plan)
		{
			var ids = new List<string>();
			foreach (var item in plan) {
				if (item.IsNormal && !item.Passthrough) {
					ids.Add(item.Source.Id);
				}
			}
			return ids.ToArray();
		}
	}
}
