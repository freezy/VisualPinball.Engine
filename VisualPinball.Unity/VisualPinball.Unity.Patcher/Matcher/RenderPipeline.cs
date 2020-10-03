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
				return RenderPipelineType.BuiltIn; }
		}
	}
}
