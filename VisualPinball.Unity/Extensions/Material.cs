using UnityEngine;
using VisualPinball.Unity.Importer;

namespace VisualPinball.Unity.Extensions
{
	public static class Material
	{

		private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

		public static UnityEngine.Material ToUnityMaterial(this VisualPinball.Engine.VPT.Material vpxMaterial)
		{
			Logger.Info("material " + vpxMaterial.Name);
			 Logger.Info("BaseColor " + vpxMaterial.BaseColor);
			 Logger.Info("Roughness " + vpxMaterial.Roughness);
			 Logger.Info("Glossiness " + vpxMaterial.Glossiness);
			 Logger.Info("GlossyImageLerp " + vpxMaterial.GlossyImageLerp);
			 Logger.Info("Thickness " + vpxMaterial.Thickness);
			 Logger.Info("ClearCoat " + vpxMaterial.ClearCoat);
			 Logger.Info("Opacity " + vpxMaterial.Opacity);
			 Logger.Info("IsOpacityActive " + vpxMaterial.IsOpacityActive);
			 Logger.Info("IsMetal " + vpxMaterial.IsMetal);
			 Logger.Info("Edge " + vpxMaterial.Edge);
			 Logger.Info("EdgeAlpha " + vpxMaterial.EdgeAlpha);
			 Logger.Info("------------------------------------");

			UnityEngine.Material generatedUnityMaterial = new UnityEngine.Material(Shader.Find("Standard"));
			generatedUnityMaterial.name = vpxMaterial.Name;
			generatedUnityMaterial.SetColor("_Color", vpxMaterial.BaseColor.ToUnityColor());
			if (vpxMaterial.IsMetal)
			{
				generatedUnityMaterial.SetFloat("_Metallic", 1f);
			}
			generatedUnityMaterial.SetFloat("_Glossiness", vpxMaterial.Roughness);
			if (vpxMaterial.Opacity < 1f && vpxMaterial.IsOpacityActive) {				
				generatedUnityMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
				generatedUnityMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
				generatedUnityMaterial.SetInt("_ZWrite", 0);
				generatedUnityMaterial.DisableKeyword("_ALPHATEST_ON");
				generatedUnityMaterial.DisableKeyword("_ALPHABLEND_ON");
				generatedUnityMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
				generatedUnityMaterial.renderQueue = 3000;
				generatedUnityMaterial.SetFloat("_Mode", 3);
			}
			return generatedUnityMaterial;
		}

		public static string GetUnityFilename(this VisualPinball.Engine.VPT.Material vpxMaterial, string folderName, string objectName)
		{
			return $"{folderName}/{AssetUtility.StringToFilename(objectName)}_{AssetUtility.StringToFilename(vpxMaterial.Name)}.mat";
		}

	}
}
