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

		// The mode-137 kernels run one block per thread group, and D3D caps dispatches at 65535
		// groups per dimension. The g_start_block_id constant exists exactly for this batching.
		private const int MaxBlocksPerDispatch = 32768;

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
		/// Encodes one mip level of <paramref name="source"/> (an RGBA32 texture with the mip data
		/// already uploaded) to BC7 and asynchronously copies the blocks into
		/// <paramref name="destination"/> at <paramref name="destinationOffset"/>. The texture must
		/// stay alive until the readback completes; track that via the returned request or
		/// <paramref name="onCompleted"/> (invoked on the main thread with success=false on GPU
		/// readback errors).
		/// </summary>
		public AsyncGPUReadbackRequest EncodeMip(
			Texture source,
			int sourceMip,
			int mipWidth,
			int mipHeight,
			byte[] destination,
			int destinationOffset,
			Action<bool> onCompleted)
		{
			if (!_initialized) {
				throw new InvalidOperationException("VpeBc7GpuEncoder is not initialized.");
			}

			var blocksX = Mathf.Max(1, (mipWidth + 3) / 4);
			var blocksY = Mathf.Max(1, (mipHeight + 3) / 4);
			var numBlocks = blocksX * blocksY;
			EnsureCapacity(numBlocks);

			_shader.SetInt(TexWidthId, mipWidth);
			_shader.SetInt(TexHeightId, mipHeight);
			_shader.SetInt(NumBlockXId, blocksX);
			_shader.SetInt(FormatId, Bc7FormatId);
			_shader.SetInt(NumTotalBlocksId, numBlocks);
			_shader.SetFloat(AlphaWeightId, 1f);
			_shader.SetInt(SrcMipId, sourceMip);

			for (var startBlock = 0; startBlock < numBlocks; startBlock += MaxBlocksPerDispatch) {
				var batch = Mathf.Min(MaxBlocksPerDispatch, numBlocks - startBlock);
				_shader.SetInt(StartBlockId, startBlock);

				// Modes 4/5/6 (single subset): 4 blocks per 64-thread group, results into err1.
				_shader.SetInt(ModeIdId, 0);
				BindKernel(_kernelTryMode456, source, _err2, _err1);
				_shader.Dispatch(_kernelTryMode456, Mathf.Max(1, (batch + 3) / 4), 1, 1);

				// Modes 1/3/7 (two subsets): one block per group, ping-ponging the error buffers
				// the same way DirectXTex does (err1 -> err2 -> err1 -> err2).
				for (var i = 0; i < 3; i++) {
					_shader.SetInt(ModeIdId, Bc7TwoSubsetModes[i]);
					var input = (i & 1) == 1 ? _err2 : _err1;
					var output = (i & 1) == 1 ? _err1 : _err2;
					BindKernel(_kernelTryMode137, source, input, output);
					_shader.Dispatch(_kernelTryMode137, batch, 1, 1);
				}

				// Final packing of the best candidate per block; err2 holds the last results.
				BindKernel(_kernelEncodeBlock, source, _err2, _output);
				_shader.Dispatch(_kernelEncodeBlock, Mathf.Max(1, (batch + 3) / 4), 1, 1);
			}

			var byteCount = numBlocks * 16;
			return AsyncGPUReadback.Request(_output, byteCount, 0, request => {
				if (request.hasError) {
					Logger.Warn("VpeBc7GpuEncoder: GPU readback failed.");
					onCompleted?.Invoke(false);
					return;
				}
				var data = request.GetData<byte>();
				NativeArray<byte>.Copy(data, 0, destination, destinationOffset, byteCount);
				onCompleted?.Invoke(true);
			});
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
