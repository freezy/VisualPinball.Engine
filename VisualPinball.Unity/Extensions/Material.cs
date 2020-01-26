// ReSharper disable StringLiteralTypo

using System;
using UnityEngine;
using VisualPinball.Unity.Importer;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.IO;

namespace VisualPinball.Unity.Extensions
{
	public static class Material
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

		public static UnityEngine.Material ToUnityMaterial(this VisualPinball.Engine.VPT.Material vpxMaterial, RenderObject ro)
		{
			Profiler.Start("Material.ToUnityMaterial()");
			var unityMaterial = new UnityEngine.Material(Shader.Find("Standard")) {
				name = vpxMaterial.Name
			};

			// color
			var col = vpxMaterial.BaseColor.ToUnityColor();
			// TODO re-enable or remove
			// if (vpxMaterial.BaseColor.IsGray() && col.grayscale > 0.8) {
			// 	// we dont want bright or solid white colors, never good for CG
			// 	col.r = col.g = col.b = 0.8f;
			// }
			unityMaterial.SetColor(Color, col);

			// metal and glossiness
			if (vpxMaterial.IsMetal) {
				unityMaterial.SetFloat(Metallic, 1f);
			}
			unityMaterial.SetFloat(Glossiness, vpxMaterial.Roughness);

			// blend modes
			var blendMode = BlendMode.Opaque;
			if (vpxMaterial.IsOpacityActive) {
				col.a = Mathf.Min(1, Mathf.Max(0, vpxMaterial.Opacity));
				unityMaterial.SetColor(Color, col);
				blendMode = BlendMode.Transparent;
			}
			if (vpxMaterial.Edge < 1) {
				blendMode = BlendMode.Cutout;

			}
			if (blendMode == BlendMode.Opaque && ro.Map != null) {
				// if we cannot determine transparency or cutout through material
				// props, look at the texture.
				Profiler.Start("GetStats()");
				var stats = ro.Map.GetStats(1000);
				Profiler.Stop("GetStats()");
				if (!stats.IsOpaque) {
					blendMode = stats.Translucent / stats.Transparent > 0.1
						? BlendMode.Transparent
						: BlendMode.Cutout;
				}
			}

			// normal map
			if (ro.NormalMap != null) {
				unityMaterial.EnableKeyword("_NORMALMAP");
			}

			// blend mode
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
			Profiler.Stop("Material.ToUnityMaterial()");
			return unityMaterial;
		}

		public static string GetUnityFilename(this VisualPinball.Engine.VPT.Material vpxMaterial, string folderName, string objectName)
		{
			return $"{folderName}/{AssetUtility.StringToFilename(objectName)}_{AssetUtility.StringToFilename(vpxMaterial.Name)}.mat";
		}

	}
}
