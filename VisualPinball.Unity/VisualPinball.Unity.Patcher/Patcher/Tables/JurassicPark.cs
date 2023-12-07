// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

// ReSharper disable StringLiteralTypo
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using UnityEngine;
using UnityEngine.Rendering;

namespace VisualPinball.Unity.Patcher
{
	[MetaMatch(TableName = "Jurassic Park (Data East)", AuthorName = "Dark & Friends")]
	public class JurassicPark : TablePatcher
	{
		#region Shader Properties

		private static readonly int DistortionSrcBlend = Shader.PropertyToID("_DistortionSrcBlend");
		private static readonly int DistortionDstBlend = Shader.PropertyToID("_DistortionDstBlend");
		private static readonly int DistortionBlurSrcBlend = Shader.PropertyToID("_DistortionBlurSrcBlend");
		private static readonly int DistortionBlurDstBlend = Shader.PropertyToID("_DistortionBlurDstBlend");
		private static readonly int StencilWriteMask = Shader.PropertyToID("_StencilWriteMask");
		private static readonly int StencilWriteMaskGBuffer = Shader.PropertyToID("_StencilWriteMaskGBuffer");
		private static readonly int StencilWriteMaskMv = Shader.PropertyToID("_StencilWriteMaskMV");
		private static readonly int AlphaCutoffEnable = Shader.PropertyToID("_AlphaCutoffEnable");
		private static readonly int TransparentDepthPrepassEnable = Shader.PropertyToID("_TransparentDepthPrepassEnable");
		private static readonly int SurfaceType = Shader.PropertyToID("_SurfaceType");
		private static readonly int BlendMode = Shader.PropertyToID("_BlendMode");
		private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
		private static readonly int AlphaDstBlend = Shader.PropertyToID("_AlphaDstBlend");
		private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
		private static readonly int ZTestDepthEqualForOpaque = Shader.PropertyToID("_ZTestDepthEqualForOpaque");
		private static readonly int ZTestGBuffer = Shader.PropertyToID("_ZTestGBuffer");

		#endregion

		/// <summary>
		/// Removing the normal map.
		/// The normal map of the TRex Head is bad and contains invalid data.
		/// This causes the entire unity editor window to become black and the play mode flicker if normal map scale is higher than 0.
		/// </summary>
		/// <param name="gameObject"></param>
		[NameMatch("TrexMain")]
		public void FixBrokenNormalMap(GameObject gameObject)
		{
			RenderPipeline.Current.MaterialAdapter.SetNormalMapDisabled(gameObject);
		}

		[NameMatch("LFLogo", Ref="Playfield/Flippers/LeftFlipper")]
		[NameMatch("RFLogo", Ref="Playfield/Flippers/RightFlipper")]
		[NameMatch("RFLogo1", Ref="Playfield/Flippers/UpperRightFlipper")]
		public void ReparentFlippers(PrimitiveComponent primitive, GameObject gameObject, ref GameObject parent)
		{
			PatcherUtil.Reparent(gameObject, parent);
			primitive.Position = Vector3.zero;
			// primitive.ObjectRotation.z = 0;
		}

		[NameMatch("PLeftFlipper")]
		[NameMatch("PRightFlipper")]
		[NameMatch("PRightFlipper1")]
		public void SetAlphaCutOffEnabled(GameObject gameObject)
		{
			RenderPipeline.Current.MaterialAdapter.SetAlphaCutOffEnabled(gameObject);
		}

		[NameMatch("Primitive_Plastics")]
		public void SetOpaque(GameObject gameObject)
		{
			RenderPipeline.Current.MaterialAdapter.SetOpaque(gameObject);
		}

		[NameMatch("leftrail")]
		[NameMatch("rightrail")]
		[NameMatch("TrexMain")]
		[NameMatch("sidewalls")]
		public void SetDoubleSided(GameObject gameObject)
		{
			RenderPipeline.Current.MaterialAdapter.SetDoubleSided(gameObject);
		}

		[NameMatch("Primitive_SideWallReflect")]
		[NameMatch("Primitive_SideWallReflect1")]
		[NameMatch("Primitive_PlasticWhitePart")] // bug in the table, sticks out from the ramp; doesn't contribute anyway to the table
		public void Hide(GameObject gameObject)
		{
			PatcherUtil.Hide(gameObject);
		}

		/// <summary>
		/// Custom properties for the ramp:
		/// * change opaque to transparent
		/// * disable alpha clipping
		/// * enable depth prepass
		/// </summary>
		/// <param name="gameObject"></param>
		[NameMatch("Primitive_PlasticsRamp")]
		public void ApplyRampSettings(GameObject gameObject)
		{
			var material = gameObject.GetComponent<Renderer>().sharedMaterial;

			material.EnableKeyword("_BLENDMODE_PRESERVE_SPECULAR_LIGHTING");
			material.EnableKeyword("_BLENDMODE_PRE_MULTIPLY");
			material.EnableKeyword("_ENABLE_FOG_ON_TRANSPARENT");
			material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.EnableKeyword("_DISABLE_SSR_TRANSPARENT");
			material.DisableKeyword("_ALPHATEST_ON");

			material.SetInt(DistortionSrcBlend, 1);
			material.SetInt(DistortionDstBlend, 1);
			material.SetInt(DistortionBlurSrcBlend, 1);
			material.SetInt(DistortionBlurDstBlend, 1);
			material.SetInt(StencilWriteMask, 6);
			material.SetInt(StencilWriteMaskGBuffer, 14);
			material.SetInt(StencilWriteMaskMv, 40);

			material.SetInt(AlphaCutoffEnable, 0);
			material.SetInt(TransparentDepthPrepassEnable, 1);
			material.SetInt(SurfaceType, 1);
			material.SetInt(BlendMode, 4);
			material.SetInt(DstBlend, 10);
			material.SetInt(AlphaDstBlend, 10);
			material.SetInt(ZWrite, 0);
			material.SetInt(ZTestDepthEqualForOpaque, 4);
			material.SetInt(ZTestGBuffer, 4);

			material.SetShaderPassEnabled("TransparentDepthPrepass", true);
			material.SetShaderPassEnabled("RayTracingPrepass", false);

			material.renderQueue = (int) RenderQueue.Transparent;
		}
	}
}
