// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using VisualPinball.Unity.Patcher.RenderPipelinePatcher;

namespace VisualPinball.Unity.Patcher.Matcher
{
	public enum RenderPipelineType
	{
		BuiltIn,
		Hdrp,
		Urp
	}

	public static class RenderPipeline
	{
		public static RenderPipelineType Current
		{
			get { if (UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset != null) {

					if (UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset.GetType().Name.Contains("UniversalRenderPipelineAsset")) {
						return RenderPipelineType.Urp;
					}

					if (UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset.GetType().Name.Contains("HDRenderPipelineAsset")) {
						return RenderPipelineType.Hdrp;
					}
				}
				return RenderPipelineType.BuiltIn; 
			}
		}

		/// <summary>
		/// Patcher instance for the current graphics pipeline
		/// </summary>
		public static IRenderPipelinePatcher Patcher => CreateRenderPipelinePatcher();

		/// <summary>
		/// Create a render pipeline patcher depending on the graphics pipeline
		/// </summary>
		/// <returns></returns>
		private static IRenderPipelinePatcher CreateRenderPipelinePatcher()
		{
			switch (RenderPipeline.Current)
			{
				case RenderPipelineType.BuiltIn:
					return new BuiltInPatcher();
				case RenderPipelineType.Hdrp:
					return new HdrpPatcher();
				case RenderPipelineType.Urp:
					return new UrpPatcher();
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}
}
