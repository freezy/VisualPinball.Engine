using UnityEngine;

namespace VisualPinball.Unity.Extensions
{
	public static class Material
	{

		public static UnityEngine.Material ToUnityMaterial(this VisualPinball.Engine.VPT.Material vpxMaterial)
		{
			Debug.Log("material " + vpxMaterial.Name);
			Debug.Log("BaseColor " + vpxMaterial.BaseColor);
			Debug.Log("Roughness " + vpxMaterial.Roughness);
			Debug.Log("Glossiness " + vpxMaterial.Glossiness);			
			Debug.Log("GlossyImageLerp " + vpxMaterial.GlossyImageLerp);
			Debug.Log("Thickness " + vpxMaterial.Thickness);			
			Debug.Log("ClearCoat " + vpxMaterial.ClearCoat);
			Debug.Log("Opacity " + vpxMaterial.Opacity);
			Debug.Log("IsOpacityActive " + vpxMaterial.IsOpacityActive);
			Debug.Log("IsMetal " + vpxMaterial.IsMetal);			
			Debug.Log("Edge " + vpxMaterial.Edge);			
			Debug.Log("EdgeAlpha " + vpxMaterial.EdgeAlpha);
			Debug.Log("------------------------------------");

			UnityEngine.Material generatedUnityMaterial = new UnityEngine.Material(Shader.Find("Standard"));
			generatedUnityMaterial.name = vpxMaterial.Name;
			float r = (vpxMaterial.BaseColor >> 16) & 0xff;
			float g = (vpxMaterial.BaseColor >> 8) & 0xff;
			float b = (vpxMaterial.BaseColor >> 0) & 0xff;
			generatedUnityMaterial.SetColor("_Color", new UnityEngine.Color(r / 255f, g / 255f, b / 255f));
			if (vpxMaterial.IsMetal)
			{
				generatedUnityMaterial.SetFloat("_Metallic", 1f);
			}
			generatedUnityMaterial.SetFloat("_Glossiness", vpxMaterial.Glossiness);
			return generatedUnityMaterial;
		}

	}
}
