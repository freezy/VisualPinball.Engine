using System;
using System.Text;
using UnityEngine;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	public class HdrpMaterialConverter : IMaterialConverter
	{
		#region Shader Properties

		private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
		private static readonly int Metallic = Shader.PropertyToID("_Metallic");
		private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
		private static readonly int BaseColorMap = Shader.PropertyToID("_BaseColorMap");
		private static readonly int NormalMapSpace = Shader.PropertyToID("_NormalMapSpace");
		private static readonly int NormalScale = Shader.PropertyToID("_NormalScale");
		private static readonly int NormalMap = Shader.PropertyToID("_NormalMap");
		private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
		private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
		private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
		private static readonly int SurfaceType = Shader.PropertyToID("_SurfaceType");
		private static readonly int AlphaCutoffEnable = Shader.PropertyToID("_AlphaCutoffEnable");
		private static readonly int ZTestDepthEqualForOpaque = Shader.PropertyToID("_ZTestDepthEqualForOpaque");
		private static readonly int ZTestModeDistortion = Shader.PropertyToID("_ZTestModeDistortion");
		private static readonly int ZTestGBuffer = Shader.PropertyToID("_ZTestGBuffer");
		private static readonly int BlendMode = Shader.PropertyToID("_BlendMode");
		private static readonly int TransparentSortPriority = Shader.PropertyToID("_TransparentSortPriority");
		private static readonly int MainTex = Shader.PropertyToID("_MainTex");
		private static readonly int Color = Shader.PropertyToID("_Color");
		private static readonly int AlphaCutoff = Shader.PropertyToID("_AlphaCutoff");
		private static readonly int Cutoff = Shader.PropertyToID("_Cutoff");

		#endregion

		public Shader GetShader()
		{
			return Shader.Find("HDRP/Lit");
		}

		public UnityEngine.Material CreateMaterial(PbrMaterial vpxMaterial, TableAuthoring table, StringBuilder debug = null)
		{
			var unityMaterial = new UnityEngine.Material(GetShader())
			{
				name = vpxMaterial.Id
			};

			// apply some basic manipulations to the color. this just makes very
			// very white colors be clipped to 0.8204 aka 204/255 is 0.8
			// this is to give room to lighting values. so there is more modulation
			// of brighter colors when being lit without blow outs too soon.
			var col = vpxMaterial.Color.ToUnityColor();
			if (vpxMaterial.Color.IsGray() && col.grayscale > 0.8)
			{
				debug?.AppendLine("Color manipulation performed, brightness reduced.");
				col.r = col.g = col.b = 0.8f;
			}

			// alpha for color depending on blend mode
			ApplyBlendMode(unityMaterial, vpxMaterial.MapBlendMode);
			if (vpxMaterial.MapBlendMode == Engine.VPT.BlendMode.Translucent)
			{
				col.a = Mathf.Min(1, Mathf.Max(0, vpxMaterial.Opacity));
			}
			unityMaterial.SetColor(BaseColor, col);

			// validate IsMetal. if true, set the metallic value.
			// found VPX authors setting metallic as well as translucent at the
			// same time, which does not render correctly in unity so we have
			// to check if this value is true and also if opacity <= 1.
			if (vpxMaterial.IsMetal && (!vpxMaterial.IsOpacityActive || vpxMaterial.Opacity >= 1))
			{
				unityMaterial.SetFloat(Metallic, 1f);
				debug?.AppendLine("Metallic set to 1.");
			}

			// roughness / glossiness
			unityMaterial.SetFloat(Smoothness, vpxMaterial.Roughness);

			// map
			if (table != null && vpxMaterial.HasMap)
			{
				unityMaterial.SetTexture(BaseColorMap,table.GetTexture(vpxMaterial.Map.Name));
			}

			// normal map
			if (table != null && vpxMaterial.HasNormalMap)
			{
				unityMaterial.EnableKeyword("_NORMALMAP");
				unityMaterial.EnableKeyword("_NORMALMAP_TANGENT_SPACE");

				unityMaterial.SetInt(NormalMapSpace, 0); // 0 = TangentSpace, 1 = ObjectSpace
				unityMaterial.SetFloat(NormalScale, 0f); // TODO FIXME: setting the scale to 0 for now. anything above 0 makes the entire unity editor window become black which is more likely a unity bug

				unityMaterial.SetTexture( NormalMap, table.GetTexture(vpxMaterial.NormalMap.Name));
			}

			// GI hack. This is a necessary step, see respective code in BaseUnlitGUI.cs of the HDRP source
			SetupMainTexForAlphaTestGI(unityMaterial, "_BaseColorMap", "_BaseColor");

			return unityMaterial;
		}

		private static void ApplyBlendMode(UnityEngine.Material unityMaterial, BlendMode blendMode)
		{
			// disable shader passes
			unityMaterial.SetShaderPassEnabled("DistortionVectors", false);
			unityMaterial.SetShaderPassEnabled("MOTIONVECTORS", false);
			unityMaterial.SetShaderPassEnabled("TransparentDepthPrepass", false);
			unityMaterial.SetShaderPassEnabled("TransparentDepthPostpass", false);
			unityMaterial.SetShaderPassEnabled("TransparentBackface", false);

			// reset existing blend modes, will be enabled explicitly
			unityMaterial.DisableKeyword("_BLENDMODE_ALPHA");
			unityMaterial.DisableKeyword("_BLENDMODE_ADD");
			unityMaterial.DisableKeyword("_BLENDMODE_PRE_MULTIPLY");

			switch (blendMode)
			{
				case Engine.VPT.BlendMode.Opaque:

					// required for the blend mode
					unityMaterial.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.One);
					unityMaterial.SetInt(DstBlend, (int)UnityEngine.Rendering.BlendMode.Zero);
					unityMaterial.SetInt(ZWrite, 1);

					// properties
					unityMaterial.SetFloat(SurfaceType, 0); // 0 = Opaque; 1 = Transparent
					unityMaterial.SetFloat(AlphaCutoffEnable, 0);

					// render queue
					unityMaterial.renderQueue = -1;

					break;

				case Engine.VPT.BlendMode.Cutout:

					// set render type
					unityMaterial.SetOverrideTag("RenderType", "TransparentCutout");

					// keywords
					unityMaterial.EnableKeyword("_ALPHATEST_ON");
					unityMaterial.EnableKeyword("_NORMALMAP_TANGENT_SPACE");

					// required for the blend mode
					unityMaterial.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.One);
					unityMaterial.SetInt(DstBlend, (int)UnityEngine.Rendering.BlendMode.Zero);
					unityMaterial.SetInt(ZWrite, 1);

					// properties
					unityMaterial.SetFloat(SurfaceType, 0); // 0 = Opaque; 1 = Transparent
					unityMaterial.SetFloat(AlphaCutoffEnable, 1);

					unityMaterial.SetFloat(ZTestDepthEqualForOpaque, 3);
					unityMaterial.SetFloat(ZTestModeDistortion, 4);
					unityMaterial.SetFloat(ZTestGBuffer, 3);

					// render queue
					unityMaterial.renderQueue = 2450;

					break;

				case Engine.VPT.BlendMode.Translucent:

					// set render type
					unityMaterial.SetOverrideTag("RenderType", "Transparent");

					// keywords
					//unityMaterial.EnableKeyword("_ALPHATEST_ON"); // required for _AlphaCutoffEnable
					unityMaterial.EnableKeyword("_BLENDMODE_PRESERVE_SPECULAR_LIGHTING");
					unityMaterial.EnableKeyword("_BLENDMODE_PRE_MULTIPLY");
					unityMaterial.EnableKeyword("_ENABLE_FOG_ON_TRANSPARENT");
					unityMaterial.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
					unityMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

					// required for the blend mode
					unityMaterial.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.One);
					unityMaterial.SetInt(DstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
					unityMaterial.SetInt(ZWrite, 0);

					// properties
					unityMaterial.SetFloat(SurfaceType, 1); // 0 = Opaque; 1 = Transparent
					//unityMaterial.SetFloat("_AlphaCutoffEnable", 1); // enable keyword _ALPHATEST_ON if this is required
					unityMaterial.SetFloat(BlendMode, 4); // 0 = Alpha, 1 = Additive, 4 = PreMultiply

					// render queue
					const int transparentRenderQueueBase = 3000;
					const int transparentSortingPriority = 0;
					unityMaterial.SetInt(TransparentSortPriority, transparentSortingPriority);
					unityMaterial.renderQueue = transparentRenderQueueBase + transparentSortingPriority;

					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		// This is a hack for GI. PVR looks in the shader for a texture named "_MainTex" to extract the opacity of the material for baking. In the same manner, "_Cutoff" and "_Color" are also necessary.
		// Since we don't have those parameters in our shaders we need to provide a "fake" useless version of them with the right values for the GI to work.
		private static void SetupMainTexForAlphaTestGI(UnityEngine.Material unityMaterial, string colorMapPropertyName, string colorPropertyName)
		{
			if (unityMaterial.HasProperty(colorMapPropertyName))
			{
				var mainTex = unityMaterial.GetTexture(colorMapPropertyName);
				unityMaterial.SetTexture(MainTex, mainTex);
			}

			if (unityMaterial.HasProperty(colorPropertyName))
			{
				var color = unityMaterial.GetColor(colorPropertyName);
				unityMaterial.SetColor(Color, color);
			}

			if (unityMaterial.HasProperty("_AlphaCutoff"))
			{
				var cutoff = unityMaterial.GetFloat(AlphaCutoff);
				unityMaterial.SetFloat(Cutoff, cutoff);
			}
		}
	}
}
