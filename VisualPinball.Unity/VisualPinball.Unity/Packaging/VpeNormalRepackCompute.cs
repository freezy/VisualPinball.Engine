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
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// GPU compute repack of plain-RGB normal maps into HDRP's AG layout (1, y, 1, x) during the
	/// texture cook. Reads the source through a plain SRV (raw UNorm texels via .Load, no sRGB decode)
	/// and writes a UAV, sidestepping the sRGB-SRV decode a custom-material Graphics.Blit applies on
	/// this path. When unavailable (no compute support, or the shader fails to load), the cook falls
	/// back to the CPU repack in <see cref="VpeTextureCook"/>.
	/// </summary>
	public sealed class VpeNormalRepackCompute
	{
		private const string ShaderResourcePath = "VpeCookNormalRepack";
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private static readonly int SourceId = Shader.PropertyToID("_Source");
		private static readonly int DestId = Shader.PropertyToID("_Dest");
		private static readonly int WidthId = Shader.PropertyToID("_Width");
		private static readonly int HeightId = Shader.PropertyToID("_Height");

		private ComputeShader _shader;
		private int _kernel;
		private uint _groupX = 8;
		private uint _groupY = 8;
		private bool _initialized;

		public static bool IsSupported => SystemInfo.supportsComputeShaders;

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
				Logger.Warn($"VpeNormalRepackCompute: failed to load Resources/{ShaderResourcePath}.compute; normals will repack on the CPU.");
				return false;
			}

			try {
				_kernel = _shader.FindKernel("RepackNormal");
			} catch (Exception ex) {
				Logger.Warn(ex, "VpeNormalRepackCompute: RepackNormal kernel missing; normals will repack on the CPU.");
				return false;
			}

			_shader.GetKernelThreadGroupSizes(_kernel, out _groupX, out _groupY, out _);
			_initialized = true;
			return true;
		}

		/// <summary>
		/// AG-repacks <paramref name="source"/> into mip 0 of <paramref name="dest"/>. The destination
		/// must be a RenderTexture created with <c>enableRandomWrite = true</c>.
		/// </summary>
		public void Repack(Texture source, RenderTexture dest, int width, int height)
		{
			_shader.SetTexture(_kernel, SourceId, source);
			_shader.SetTexture(_kernel, DestId, dest);
			_shader.SetInt(WidthId, width);
			_shader.SetInt(HeightId, height);
			var groupsX = (int)(((uint)width + _groupX - 1) / _groupX);
			var groupsY = (int)(((uint)height + _groupY - 1) / _groupY);
			_shader.Dispatch(_kernel, groupsX, groupsY, 1);
		}
	}
}
