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

// ReSharper disable StringLiteralTypo

using UnityEngine;

namespace VisualPinball.Unity.Patcher
{
	[MetaMatch(TableName = "Jurassic Park (Data East)", AuthorName = "Dark & Friends")]
	public class JurassicPark
	{
		/// <summary>
		/// Removing the normal map.
		/// The normal map of the TRex Head is bad and contains invalid data.
		/// This causes the entire unity editor window to become black and the play mode flicker if normal map scale is higher than 0.
		/// </summary>
		/// <param name="gameObject"></param>
		[NameMatch("TrexMain")]
		public void FixBrokenNormalMap(GameObject gameObject)
		{
			PatcherUtil.SetNormalMapDisabled(gameObject);
		}


		[NameMatch("LFLogo", Ref="Flippers/LeftFlipper")]
		[NameMatch("RFLogo", Ref="Flippers/RightFlipper")]
		[NameMatch("RFLogo1", Ref="Flippers/UpperRightFlipper")]
		public void ReparentFlippers(GameObject gameObject, ref GameObject parent)
		{
			PatcherUtil.Reparent(gameObject, parent);
		}

		[NameMatch("PLeftFlipper")]
		[NameMatch("PRightFlipper")]
		[NameMatch("PRightFlipper1")]
		public void SetAlphaCutOffEnabled(GameObject gameObject)
		{
			PatcherUtil.SetAlphaCutOffEnabled(gameObject);
		}

		[NameMatch("Primitive_Plastics")]
		public void SetOpaque(GameObject gameObject)
		{
			PatcherUtil.SetOpaque(gameObject);
		}

		[NameMatch("leftrail")]
		[NameMatch("rightrail")]
		[NameMatch("TrexMain")]
		[NameMatch("sidewalls")]
		public void SetDoubleSided(GameObject gameObject)
		{
			PatcherUtil.SetDoubleSided(gameObject);
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

			material.SetInt("_DistortionSrcBlend", 1);
			material.SetInt("_DistortionDstBlend", 1);
			material.SetInt("_DistortionBlurSrcBlend", 1);
			material.SetInt("_DistortionBlurDstBlend", 1);
			material.SetInt("_StencilWriteMask", 6);
			material.SetInt("_StencilWriteMaskGBuffer", 14);
			material.SetInt("_StencilWriteMaskMV", 40);

			material.SetInt("_AlphaCutoffEnable", 0);
			material.SetInt("_TransparentDepthPrepassEnable", 1);
			material.SetInt("_SurfaceType", 1);
			material.SetInt("_BlendMode", 4);
			material.SetInt("_DstBlend", 10);
			material.SetInt("_AlphaDstBlend", 10);
			material.SetInt("_ZWrite", 0);
			material.SetInt("_ZTestDepthEqualForOpaque", 4);
			material.SetInt("_ZTestGBuffer", 4);

			material.SetShaderPassEnabled("TransparentDepthPrepass", true);
			material.SetShaderPassEnabled("RayTracingPrepass", false);

			material.renderQueue = (int) UnityEngine.Rendering.RenderQueue.Transparent;

		}
	}
}
