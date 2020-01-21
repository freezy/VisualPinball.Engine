using System;
using UnityEngine;
using VisualPinball.Unity.Importer;
using VisualPinball.Engine.Game;
using VisualPinball.Unity.Extensions;
namespace VisualPinball.Unity.Importer
{
	class UnityMaterialGenerator
	{


		private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

		private static readonly int Color = Shader.PropertyToID("_Color");
		private static readonly int Metallic = Shader.PropertyToID("_Metallic");
		private static readonly int Glossiness = Shader.PropertyToID("_Glossiness");
		private static readonly int Mode = Shader.PropertyToID("_Mode");
		private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
		private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
		private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");

		private enum BlendMode
		{
			Opaque,
			Cutout,
			Fade,
			Transparent
		}


		public static UnityEngine.Material ToUnityMaterial(VisualPinball.Engine.VPT.Material vpxMaterial, RenderObject ro) {
			ro.buildInfo = "";
			string newLineValue = "\r\n";			
			var unityMaterial = new UnityEngine.Material(Shader.Find("Standard")) {name = ro.MaterialIdFixed};
			ro.buildInfo +=  "id "+ro.MaterialIdFixed + newLineValue;
			// color
			var col = vpxMaterial.BaseColor.ToUnityColor();
			// TODO re-enable or remove
			// if (vpxMaterial.BaseColor.IsGray() && col.grayscale > 0.8) {
			// 	// we dont want bright or solid white colors, never good for CG
			// 	col.r = col.g = col.b = 0.8f;
			// }
			ro.buildInfo += "col " + col.ToString() + newLineValue;

			unityMaterial.SetColor(Color, col);

			// metal and glossiness
			if (vpxMaterial.IsMetal) {
				unityMaterial.SetFloat(Metallic, 1f);
			}

			ro.buildInfo += "IsMetal " + vpxMaterial.IsMetal + newLineValue;

			unityMaterial.SetFloat(Glossiness, vpxMaterial.Roughness);

			ro.buildInfo += "Roughness " + vpxMaterial.Roughness + newLineValue;
			ro.buildInfo += "IsOpacityActive " + vpxMaterial.IsOpacityActive + newLineValue;
			ro.buildInfo += "Opacity " + vpxMaterial.Opacity + newLineValue;

			// blend modes
			var blendMode = BlendMode.Opaque;
			var blendModePendingFromTextureAnalysis = BlendMode.Opaque;

			float alphaThreshold = 0.9f;

			//get texture analysis values

			if (ro.Map != null) {
				var stats = ro.Map?.GetStats(1000);
				if (stats != null && !stats.IsOpaque) {
					ro.buildInfo += "stats.Translucent " + stats.Translucent + newLineValue;
					ro.buildInfo += "stats.Transparent " + stats.Transparent + newLineValue;
					blendModePendingFromTextureAnalysis = stats.Translucent / stats.Transparent > 0.1
						? BlendMode.Transparent
						: BlendMode.Cutout;
				}
			}

			float materialOpacity = Mathf.Min(1, Mathf.Max(0, vpxMaterial.Opacity));


			if (vpxMaterial.IsOpacityActive) {

				//ifIsOpacityActive we start by filtering by some thresholding to get rid of insequential values that cause more
				//problems that what they are worth.
				if (materialOpacity < alphaThreshold) {
					ro.buildInfo += "alphaThreshold met" + newLineValue;
					col.a = materialOpacity;
					unityMaterial.SetColor(Color, col);
					blendMode = BlendMode.Transparent;
				} else {
					ro.buildInfo += "alphaThreshold NOT met" + newLineValue;
				}
			}
			if (vpxMaterial.Edge < 1) {
				blendMode = BlendMode.Cutout;
				if (ro.Map != null) {
					if (!ro.Map.HasTransparentPixels) {
						ro.buildInfo += "set to cutout but image analyis reports no tranparency , revert to opaque" + newLineValue;
						blendMode = BlendMode.Opaque;
					}
				}

			}

			ro.buildInfo += "blendMode " + blendMode + newLineValue;

			if (blendMode == BlendMode.Transparent || vpxMaterial.IsOpacityActive) {
				//this here is to allow cutouts to be prioritized above transparent based on texture analysis
				if (blendModePendingFromTextureAnalysis != BlendMode.Opaque) {
					blendMode = blendModePendingFromTextureAnalysis;
				}
			}


			ro.buildInfo += "blendModePendingFromTextureAnalysis " + blendModePendingFromTextureAnalysis + newLineValue;
			ro.buildInfo += "final blendMode " + blendMode + newLineValue;


			// normal map
			if (ro.NormalMap != null) {
				ro.buildInfo += "has normal map  = true" + newLineValue;
				unityMaterial.EnableKeyword("_NORMALMAP");
			} else {
				ro.buildInfo += "has normal map  = false" + newLineValue;
			}

			ro.buildInfo += "********************" + newLineValue + newLineValue;

			SetMaterialShading(blendMode, unityMaterial);
			return unityMaterial;
		}



		public static UnityEngine.Material ToUnityMaterial(RenderObject ro) {
			ro.buildInfo = "";
			string newLineValue = "\r\n";
			var unityMaterial = new UnityEngine.Material(Shader.Find("Standard")) {
				name = ro.MaterialIdFixed
			};

			ro.buildInfo += "id " + ro.MaterialIdFixed + newLineValue;
			ro.buildInfo += "NO VPX MATERIAL BACKING " + newLineValue;
			// blend modes
			var blendMode = BlendMode.Opaque;


			if (ro.Map != null) {
				var stats = ro.Map?.GetStats(1000);
				if (stats != null && !stats.IsOpaque) {
					ro.buildInfo += "stats.Translucent " + stats.Translucent + newLineValue;
					ro.buildInfo += "stats.Transparent " + stats.Transparent + newLineValue;
					blendMode = stats.Translucent / stats.Transparent > 0.1
						? BlendMode.Transparent
						: BlendMode.Cutout;
				}
			}
		
			ro.buildInfo += "final blendMode " + blendMode + newLineValue;


			// normal map
			if (ro.NormalMap != null) {
				ro.buildInfo += "has normal map  = true" + newLineValue;
				unityMaterial.EnableKeyword("_NORMALMAP");
			} else {
				ro.buildInfo += "has normal map  = false" + newLineValue;
			}

			SetMaterialShading(blendMode, unityMaterial);

			ro.buildInfo += "********************" + newLineValue + newLineValue;

			

			return unityMaterial;
		}

		private static void SetMaterialShading(BlendMode mode, UnityEngine.Material unityMaterial) {

			// blend mode
			switch (mode) {
				case BlendMode.Opaque:
					unityMaterial.SetFloat(Mode, 0);
					unityMaterial.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.One);
					unityMaterial.SetInt(DstBlend, (int)UnityEngine.Rendering.BlendMode.Zero);
					unityMaterial.SetInt(ZWrite, 1);
					unityMaterial.DisableKeyword("_ALPHATEST_ON");
					unityMaterial.DisableKeyword("_ALPHABLEND_ON");
					unityMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
					unityMaterial.renderQueue = -1;
					break;

				case BlendMode.Cutout:
					unityMaterial.SetFloat(Mode, 1);
					unityMaterial.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.One);
					unityMaterial.SetInt(DstBlend, (int)UnityEngine.Rendering.BlendMode.Zero);
					unityMaterial.SetInt(ZWrite, 1);
					unityMaterial.EnableKeyword("_ALPHATEST_ON");
					unityMaterial.DisableKeyword("_ALPHABLEND_ON");
					unityMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
					unityMaterial.renderQueue = 2450;
					break;

				case BlendMode.Fade:
					unityMaterial.SetFloat(Mode, 2);
					unityMaterial.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
					unityMaterial.SetInt(DstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
					unityMaterial.SetInt(ZWrite, 0);
					unityMaterial.DisableKeyword("_ALPHATEST_ON");
					unityMaterial.EnableKeyword("_ALPHABLEND_ON");
					unityMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
					unityMaterial.renderQueue = 3000;
					break;

				case BlendMode.Transparent:
					unityMaterial.SetFloat(Mode, 3);
					unityMaterial.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.One);
					unityMaterial.SetInt(DstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
					//!!!!!!! this is normally switched off but somehow enabling it seems to resolve so many issues.. keep an eye out for weirld opacity issues
					//unityMaterial.SetInt("_ZWrite", 0);
					unityMaterial.SetInt(ZWrite, 1);
					unityMaterial.DisableKeyword("_ALPHATEST_ON");
					unityMaterial.DisableKeyword("_ALPHABLEND_ON");
					unityMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
					unityMaterial.renderQueue = 3000;
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

	}


}
