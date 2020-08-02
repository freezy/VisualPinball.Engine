using UnityEngine;
using VisualPinball.Resources;

namespace VisualPinball.Unity.Import.Material
{
	public class StandardBallMaterial : IBallMaterial
	{
		private readonly int MainTex = Shader.PropertyToID("_MainTex");
		private readonly int Metallic = Shader.PropertyToID("_Metallic");
		private readonly int Glossiness = Shader.PropertyToID("_Glossiness");

		public Shader GetShader()
		{
			return Shader.Find("Standard");
		}

		public UnityEngine.Material CreateMaterial()
		{
			var material = new UnityEngine.Material(GetShader());
			var texture = new Texture2D(512, 512, TextureFormat.RGBA32, true) { name = "BallDebugTexture" };
			texture.LoadImage(Resource.BallDebug.Data);
			material.SetTexture(MainTex, texture);
			material.SetFloat(Metallic, 1f);
			material.SetFloat(Glossiness, 1f);
			return material;
		}
	}
}
