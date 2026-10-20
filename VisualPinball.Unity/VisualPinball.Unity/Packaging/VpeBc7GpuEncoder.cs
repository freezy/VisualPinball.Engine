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
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// GPU BC7 encoder driving the DirectXTex BC7 compute shaders (see
	/// Resources/VpeBc7Encode.compute). Encodes RGBA32 mips into BC7 blocks and reads the result
	/// back asynchronously into a caller-provided buffer. Quality matches DirectXTex's default
	/// GPU path (modes 1, 3, 4, 5, 6, 7; the rarely-used 3-subset modes 0/2 are skipped).
	/// </summary>
	public sealed class VpeBc7GpuEncoder : IDisposable
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private const string ShaderResourcePath = "VpeBc7Encode";

		private static readonly int TexWidthId = Shader.PropertyToID("g_tex_width");
		private static readonly int TexHeightId = Shader.PropertyToID("g_tex_height");
		private static readonly int NumBlockXId = Shader.PropertyToID("g_num_block_x");
		private static readonly int FormatId = Shader.PropertyToID("g_format");
		private static readonly int ModeIdId = Shader.PropertyToID("g_mode_id");
		private static readonly int StartBlockId = Shader.PropertyToID("g_start_block_id");
		private static readonly int NumTotalBlocksId = Shader.PropertyToID("g_num_total_blocks");
		private static readonly int AlphaWeightId = Shader.PropertyToID("g_alpha_weight");
		private static readonly int SrcMipId = Shader.PropertyToID("g_src_mip");
		private static readonly int InputId = Shader.PropertyToID("g_Input");
		private static readonly int InBuffId = Shader.PropertyToID("g_InBuff");
		private static readonly int OutBuffId = Shader.PropertyToID("g_OutBuff");

		private const int Bc7FormatId = 98; // DXGI_FORMAT_BC7_UNORM, as expected by the shader

		private ComputeShader _shader;
		private int _kernelTryMode456;
		private int _kernelTryMode137;
		private int _kernelEncodeBlock;
		private ComputeBuffer _err1;
		private ComputeBuffer _err2;
		private ComputeBuffer _output;
		private int _capacityBlocks;
		private bool _initialized;
		// GPU submission size and UI frame time are separate budgets. Waiting a whole
		// rendered frame after every small batch needlessly throttles cooking to FPS.
		private sealed class PendingReadback
		{
			public AsyncGPUReadbackRequest Request;
			public Action<AsyncGPUReadbackRequest> Complete;
		}
		private bool _hasYielded;
		private readonly System.Diagnostics.Stopwatch _yieldClock = System.Diagnostics.Stopwatch.StartNew();

		public static bool IsSupported => SystemInfo.supportsComputeShaders
			&& SystemInfo.SupportsTextureFormat(TextureFormat.BC7);

		public bool Initialize()
		{
			if (_initialized) {
				return true;
			}
			if (!IsSupported) {
				return false;
			}

			_shader = Resources.Load<ComputeShader>(ShaderResourcePath);
			if (!_shader) {
				Logger.Warn($"VpeBc7GpuEncoder: failed to load Resources/{ShaderResourcePath}.");
				return false;
			}

			try {
				_kernelTryMode456 = _shader.FindKernel("TryMode456CS");
				_kernelTryMode137 = _shader.FindKernel("TryMode137CS");
				_kernelEncodeBlock = _shader.FindKernel("EncodeBlockCS");
			} catch (Exception ex) {
				Logger.Warn(ex, "VpeBc7GpuEncoder: BC7 compute kernels missing.");
				return false;
			}

			_initialized = true;
			return true;
		}

		/// <summary>
		/// Encodes with bounded GPU submissions. A player-loop yield alone does not drain
		/// the GPU queue: flush a bounded window of work, grouping small mips together.
		/// The source and buffers stay alive until the last submitted request completes,
		/// including when cancellation arrives during a readback. One encoder must not
		/// run concurrent encodes.
		/// </summary>
		public Task<bool> EncodeMipAsync(Texture source, int sourceMip, int mipWidth, int mipHeight,
			byte[] destination, int destinationOffset, CancellationToken cancellationToken)
			=> EncodeMipsAsync(source, sourceMip, new[] { mipWidth }, new[] { mipHeight },
				new[] { (long)destinationOffset }, destination, cancellationToken);

		public Task<bool> EncodeTextureAsync(Texture source, int[] widths, int[] heights, long[] offsets,
			byte[] destination, CancellationToken cancellationToken)
			=> EncodeMipsAsync(source, 0, widths, heights, offsets, destination, cancellationToken);

		private async Task<bool> EncodeMipsAsync(Texture source, int firstMip, int[] widths, int[] heights,
			long[] offsets, byte[] destination, CancellationToken cancellationToken)
		{
			if (!_initialized) throw new InvalidOperationException("VpeBc7GpuEncoder is not initialized.");
			if (source == null || destination == null) throw new ArgumentNullException();
			if (widths.Length != heights.Length || widths.Length != offsets.Length) throw new ArgumentException("Mip arrays differ in length.");
			var maximumBlocks = 0;
			for (var mip = 0; mip < widths.Length; mip++) {
				if (widths[mip] <= 0 || heights[mip] <= 0) throw new ArgumentOutOfRangeException(nameof(widths));
				var blocks = checked(((widths[mip] + 3) / 4) * ((heights[mip] + 3) / 4));
				var bytes = checked(blocks * 16);
				if (offsets[mip] < 0 || offsets[mip] > destination.Length - bytes) throw new ArgumentOutOfRangeException(nameof(offsets));
				maximumBlocks = Mathf.Max(maximumBlocks, blocks);
			}
			// Never reallocate buffers with readbacks in flight. GPU copies are queued
			// before subsequent dispatches reuse these buffers for another mip.
			EnsureCapacity(maximumBlocks);
			const int batchBlocks = 16384;
			var pending = 0;
			var requests = new List<PendingReadback>();
			var queuedBlocks = 0;
			var success = true;
			async Task Drain()
			{
				// Do not free GPU resources on cancellation/timeout while a job owns them.
				// The independent player watchdog diagnoses a genuinely hung device.
				if (pending == 0) { queuedBlocks = 0; return; }
				// Flush this bounded submission explicitly. A menu with no scene cameras
				// may not otherwise submit compute/readback commands while awaiting them.
				// Unlike the old end-of-cook wait, at most 16384 blocks are queued here.
				AsyncGPUReadback.WaitAllRequests();
				// Some players defer callbacks until the next rendered frame even though
				// the request is complete. Consume completed data now, exactly once, so
				// readback callback scheduling does not throttle every batch to frame rate.
				foreach (var work in requests) if (work.Request.done) work.Complete(work.Request);
				while (pending > 0) await Task.Yield();
				requests.Clear();
				queuedBlocks = 0;
				if (!_hasYielded || _yieldClock.ElapsedMilliseconds >= 8) {
					_hasYielded = true;
					await Task.Yield();
					_yieldClock.Restart();
				}
			}
			try {
				for (var mip = 0; mip < widths.Length; mip++) {
					var blocksX = (widths[mip] + 3) / 4;
					var numBlocks = blocksX * ((heights[mip] + 3) / 4);
					for (var start = 0; start < numBlocks; start += batchBlocks) {
						if (cancellationToken.IsCancellationRequested) {
							await Drain(); cancellationToken.ThrowIfCancellationRequested();
						}
						var count = Mathf.Min(batchBlocks, numBlocks - start);
						if (queuedBlocks + count > batchBlocks) await Drain();
						if (!success) return false;
						if (start % (batchBlocks * 64) == 0)
							Logger.Debug("BC7: {Texture} mip {Mip}, blocks {Start}/{Total}", source.name, firstMip + mip, start, numBlocks);
						_shader.SetInt(TexWidthId, widths[mip]);
						_shader.SetInt(TexHeightId, heights[mip]);
						_shader.SetInt(NumBlockXId, blocksX);
						_shader.SetInt(FormatId, Bc7FormatId);
						_shader.SetInt(NumTotalBlocksId, numBlocks);
						_shader.SetFloat(AlphaWeightId, 1f);
						_shader.SetInt(SrcMipId, firstMip + mip);
						DispatchBatch(source, start, count);
						var byteOffset = start * 16;
						var destinationOffset = (int)offsets[mip] + byteOffset;
						pending++;
						var work = new PendingReadback();
						var completed = false;
						work.Complete = request => {
							if (completed) return;
							completed = true;
							try {
								if (request.hasError) success = false;
								else NativeArray<byte>.Copy(request.GetData<byte>(), 0, destination, destinationOffset, count * 16);
							} catch (Exception ex) {
								success = false; Logger.Warn(ex, "VpeBc7GpuEncoder: batch readback failed.");
							} finally { pending--; }
						};
						work.Request = AsyncGPUReadback.Request(_output, count * 16, byteOffset, work.Complete);
						work.Request.forcePlayerLoopUpdate = true;
						requests.Add(work);
						queuedBlocks += count;
						if (queuedBlocks >= batchBlocks) await Drain();
					}
				}
			} finally { await Drain(); }
			cancellationToken.ThrowIfCancellationRequested();
			return success;
		}

		private void DispatchBatch(Texture source, int startBlock, int batch)
		{
			_shader.SetInt(StartBlockId, startBlock);
			_shader.SetInt(ModeIdId, 0);
			BindKernel(_kernelTryMode456, source, _err2, _err1);
			_shader.Dispatch(_kernelTryMode456, Mathf.Max(1, (batch + 3) / 4), 1, 1);
			for (var i = 0; i < 3; i++) {
				_shader.SetInt(ModeIdId, Bc7TwoSubsetModes[i]);
				BindKernel(_kernelTryMode137, source, (i & 1) == 1 ? _err2 : _err1, (i & 1) == 1 ? _err1 : _err2);
				_shader.Dispatch(_kernelTryMode137, batch, 1, 1);
			}
			BindKernel(_kernelEncodeBlock, source, _err2, _output);
			_shader.Dispatch(_kernelEncodeBlock, Mathf.Max(1, (batch + 3) / 4), 1, 1);
		}

		private static readonly int[] Bc7TwoSubsetModes = { 1, 3, 7 };

		private void BindKernel(int kernel, Texture source, ComputeBuffer input, ComputeBuffer output)
		{
			_shader.SetTexture(kernel, InputId, source);
			_shader.SetBuffer(kernel, InBuffId, input);
			_shader.SetBuffer(kernel, OutBuffId, output);
		}

		private void EnsureCapacity(int blocks)
		{
			if (_capacityBlocks >= blocks) {
				return;
			}

			ReleaseBuffers();
			_err1 = new ComputeBuffer(blocks, 16, ComputeBufferType.Structured);
			_err2 = new ComputeBuffer(blocks, 16, ComputeBufferType.Structured);
			_output = new ComputeBuffer(blocks, 16, ComputeBufferType.Structured);
			_capacityBlocks = blocks;
		}

		private void ReleaseBuffers()
		{
			_err1?.Release();
			_err2?.Release();
			_output?.Release();
			_err1 = _err2 = _output = null;
			_capacityBlocks = 0;
		}

		public void Dispose()
		{
			ReleaseBuffers();
			_shader = null;
			_initialized = false;
		}
	}
}
