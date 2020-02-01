﻿// ReSharper disable StringLiteralTypo

using System;
using System.Text;
using NLog;
using UnityEngine;
using UnityEngine.Rendering;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Importer.AssetHandler;
using BlendMode = VisualPinball.Engine.VPT.BlendMode;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Extensions
{
	public static class Material
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private static readonly int Color = Shader.PropertyToID("_Color");
		private static readonly int Metallic = Shader.PropertyToID("_Metallic");
		private static readonly int Glossiness = Shader.PropertyToID("_Glossiness");
		private static readonly int Mode = Shader.PropertyToID("_Mode");
		private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
		private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
		private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
		private static readonly int MainTex = Shader.PropertyToID("_MainTex");
		private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");

		public static UnityEngine.Material ToUnityMaterial(this PbrMaterial vpxMaterial, IAssetHandler assetHandler, StringBuilder debug = null)
		{
			Profiler.Start("Material.ToUnityMaterial()");
			var unityMaterial = new UnityEngine.Material(GetShader()) {
				name = vpxMaterial.Id
			};

			// apply some basic manipulations to the color. this just makes very
			// very white colors be clipped to 0.8204 aka 204/255 is 0.8
			// this is to give room to lighting values. so there is more modulation
			// of brighter colors when being lit without blow outs too soon.
			var col = vpxMaterial.Color.ToUnityColor();
			if (vpxMaterial.Color.IsGray() && col.grayscale > 0.8) {
				debug?.AppendLine("Color manipulation performed, brightness reduced.");
				col.r = col.g = col.b = 0.8f;
			}
			unityMaterial.SetColor(Color, col);

			// validate IsMetal. if true, set the metallic value.
			// found VPX authors setting metallic as well as translucent at the
			// same time, which does not render correctly in unity so we have
			// to check if this value is true and also if opacity <= 1.
			if (vpxMaterial.IsMetal && (!vpxMaterial.IsOpacityActive || vpxMaterial.Opacity >= 1)) {
				unityMaterial.SetFloat(Metallic, 1f);
				debug?.AppendLine("Metallic set to 1.");
			}

			// roughness / glossiness
			unityMaterial.SetFloat(Glossiness, vpxMaterial.Roughness);

			// blend mode
			ApplyBlendMode(unityMaterial, vpxMaterial.MapBlendMode);
			if (vpxMaterial.MapBlendMode == BlendMode.Translucent) {
				col.a = Mathf.Min(1, Mathf.Max(0, vpxMaterial.Opacity));
				unityMaterial.SetColor(Color, col);
			}

			// map
			if (vpxMaterial.HasMap) {
				unityMaterial.SetTexture(
					MainTex,
					assetHandler.LoadTexture(vpxMaterial.Map)
				);
			}

			// normal map
			if (vpxMaterial.HasNormalMap) {
				unityMaterial.EnableKeyword("_NORMALMAP");
				unityMaterial.SetTexture(
					BumpMap,
					assetHandler.LoadTexture(vpxMaterial.NormalMap)
				);
			}

			Profiler.Stop("Material.ToUnityMaterial()");
			return unityMaterial;
		}

		private static void ApplyBlendMode(UnityEngine.Material unityMaterial, BlendMode blendMode)
		{
			switch (blendMode) {
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

				case BlendMode.Translucent:
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

		private static Shader GetShader()
		{
			if (GraphicsSettings.renderPipelineAsset.GetType().Name.Contains("HDRenderPipelineAsset")) {
				return Shader.Find("Standard");
				//return Shader.Find("HDRP/Lit");

			} else {
				return Shader.Find("Standard");
			}
		}

		public static string GetUnityFilename(this PbrMaterial vpMat, string folderName)
		{
			return $"{folderName}/{vpMat.Id}.mat";
		}
	}
}
