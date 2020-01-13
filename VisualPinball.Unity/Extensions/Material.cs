using UnityEngine;
using VisualPinball.Unity.Importer;
using VisualPinball.Engine.Game;
namespace VisualPinball.Unity.Extensions
{
	public static class Material
	{

		private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

		private  enum BlendMode
		{
			Opaque,
			Cutout,
			Fade,
			Transparent

		}

		private static BlendMode blendMode = BlendMode.Opaque;

		public static UnityEngine.Material ToUnityMaterial(this VisualPinball.Engine.VPT.Material vpxMaterial, RenderObject ro) {
			blendMode = BlendMode.Opaque;

			if (ro.MaterialId ==  "Plastic with an image-plastics-clear" || ro.MaterialId =="BumperCap-bumpercap" || ro.MaterialId ==  "sides 99-sidecabL" || ro.MaterialId ==  "plastic coffin-coffinAO" || ro.MaterialId == "Plastic with opaque image 999-plastic_draca" || ro.MaterialId == "Playfield-MBX-pf" || ro.MaterialId == "plastic red flasher-dome_red" ||  ro.MaterialId == "Plastic White-coffindecal2"){
				Logger.Info("------------------------------------");
				Logger.Info("------------------------------------");
				//Logger.Info("material " + vpxMaterial.Name);
				Logger.Info("ro.MaterialId  " + ro.MaterialId);
				Logger.Info("BaseColor " + vpxMaterial.BaseColor);
				Logger.Info("Roughness " + vpxMaterial.Roughness);
				Logger.Info("Glossiness " + vpxMaterial.Glossiness);
				Logger.Info("GlossyImageLerp " + vpxMaterial.GlossyImageLerp);
				Logger.Info("Thickness " + vpxMaterial.Thickness);
				Logger.Info("ClearCoat " + vpxMaterial.ClearCoat);
				Logger.Info("Opacity " + vpxMaterial.Opacity);
				Logger.Info("IsOpacityActive " + vpxMaterial.IsOpacityActive);
				Logger.Info("OpacityActiveEdgeAlpha " + vpxMaterial.OpacityActiveEdgeAlpha);
				Logger.Info("IsMetal " + vpxMaterial.IsMetal);
				Logger.Info("Edge " + vpxMaterial.Edge);
				Logger.Info("EdgeAlpha " + vpxMaterial.EdgeAlpha);
				Logger.Info("------------------------------------");
				Logger.Info("------------------------------------");
			
			}

			


			UnityEngine.Material generatedUnityMaterial = new UnityEngine.Material(Shader.Find("Standard"));
			generatedUnityMaterial.name = vpxMaterial.Name;
			UnityEngine.Color col = vpxMaterial.BaseColor.ToUnityColor();
			generatedUnityMaterial.SetColor("_Color", col);
			if (vpxMaterial.IsMetal)
			{
				generatedUnityMaterial.SetFloat("_Metallic", 1f);
			}
			generatedUnityMaterial.SetFloat("_Glossiness", vpxMaterial.Roughness);
			if (vpxMaterial.IsOpacityActive) {
				blendMode = BlendMode.Transparent;
				col.a = vpxMaterial.Opacity;
				generatedUnityMaterial.SetColor("_Color", col);
			}
			if (vpxMaterial.Edge < 1) {
				blendMode = BlendMode.Cutout;
			}


			if (ro.NormalMap != null) {
				generatedUnityMaterial.EnableKeyword("_NORMALMAP");
			}


			switch (blendMode) {
				case BlendMode.Opaque:
					generatedUnityMaterial.SetFloat("_Mode", 0);
					generatedUnityMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
					generatedUnityMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
					generatedUnityMaterial.SetInt("_ZWrite", 1);
					generatedUnityMaterial.DisableKeyword("_ALPHATEST_ON");
					generatedUnityMaterial.DisableKeyword("_ALPHABLEND_ON");
					generatedUnityMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
					generatedUnityMaterial.renderQueue = -1;
					
					break;
				case BlendMode.Cutout:
					generatedUnityMaterial.SetFloat("_Mode", 1);
					generatedUnityMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
					generatedUnityMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
					generatedUnityMaterial.SetInt("_ZWrite", 1);
					generatedUnityMaterial.EnableKeyword("_ALPHATEST_ON");
					generatedUnityMaterial.DisableKeyword("_ALPHABLEND_ON");
					generatedUnityMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
					generatedUnityMaterial.renderQueue = 2450;
					
					break;
				case BlendMode.Fade:
					generatedUnityMaterial.SetFloat("_Mode", 2);
					generatedUnityMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
					generatedUnityMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
					generatedUnityMaterial.SetInt("_ZWrite", 0);
					generatedUnityMaterial.DisableKeyword("_ALPHATEST_ON");
					generatedUnityMaterial.EnableKeyword("_ALPHABLEND_ON");
					generatedUnityMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
					generatedUnityMaterial.renderQueue = 3000;
				
					break;
				case BlendMode.Transparent:
					generatedUnityMaterial.SetFloat("_Mode", 3);
					generatedUnityMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
					generatedUnityMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
					generatedUnityMaterial.SetInt("_ZWrite", 0);
					generatedUnityMaterial.DisableKeyword("_ALPHATEST_ON");
					generatedUnityMaterial.DisableKeyword("_ALPHABLEND_ON");
					generatedUnityMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
					generatedUnityMaterial.renderQueue = 3000;
				
					break;
			}
			


			return generatedUnityMaterial;
		}

		public static string GetUnityFilename(this VisualPinball.Engine.VPT.Material vpxMaterial, string folderName, string objectName)
		{
			return $"{folderName}/{AssetUtility.StringToFilename(objectName)}_{AssetUtility.StringToFilename(vpxMaterial.Name)}.mat";
		}

	}
}




