using UnityEngine;
using VisualPinball.Unity.Importer;

namespace VisualPinball.Unity.Extensions
{
	public static class Material
	{

		private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

		public static UnityEngine.Material ToUnityMaterial(this VisualPinball.Engine.VPT.Material vpxMaterial)
		{
			// Logger.Info("material " + vpxMaterial.Name);
			// Logger.Info("BaseColor " + vpxMaterial.BaseColor);
			// Logger.Info("Roughness " + vpxMaterial.Roughness);
			// Logger.Info("Glossiness " + vpxMaterial.Glossiness);
			// Logger.Info("GlossyImageLerp " + vpxMaterial.GlossyImageLerp);
			// Logger.Info("Thickness " + vpxMaterial.Thickness);
			// Logger.Info("ClearCoat " + vpxMaterial.ClearCoat);
			// Logger.Info("Opacity " + vpxMaterial.Opacity);
			// Logger.Info("IsOpacityActive " + vpxMaterial.IsOpacityActive);
			// Logger.Info("IsMetal " + vpxMaterial.IsMetal);
			// Logger.Info("Edge " + vpxMaterial.Edge);
			// Logger.Info("EdgeAlpha " + vpxMaterial.EdgeAlpha);
			// Logger.Info("------------------------------------");

			UnityEngine.Material generatedUnityMaterial = new UnityEngine.Material(Shader.Find("Standard"));
			generatedUnityMaterial.name = vpxMaterial.Name;
			generatedUnityMaterial.SetColor("_Color", vpxMaterial.BaseColor.ToUnityColor());
			if (vpxMaterial.IsMetal)
			{
				generatedUnityMaterial.SetFloat("_Metallic", 1f);
			}
			//generatedUnityMaterial.SetFloat("_Glossiness", vpxMaterial.Glossiness);
			return generatedUnityMaterial;
		}

		public static string GetUnityFilename(this VisualPinball.Engine.VPT.Material vpxMaterial, string folderName = null)
		{
			return folderName != null
				? $"{folderName}/{AssetUtility.StringToFilename(vpxMaterial.Name)}.mat"
				: $"{AssetUtility.StringToFilename(vpxMaterial.Name)}.mat";
		}

	}
}
