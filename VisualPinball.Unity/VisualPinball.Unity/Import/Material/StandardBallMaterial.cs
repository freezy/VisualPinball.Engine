using UnityEngine;
using VisualPinball.Resources;

namespace VisualPinball.Unity
{
	public class StandardBallMaterial : IBallMaterial
	{
		#region Shader Properties

		private static readonly int MainTex = Shader.PropertyToID("_MainTex");
		private static readonly int Metallic = Shader.PropertyToID("_Metallic");
		private static readonly int Glossiness = Shader.PropertyToID("_Glossiness");

		#endregion

		public Shader GetShader()
		{
			return Shader.Find("Standard");
		}

		public Material CreateMaterial()
		{
			var material = new Material(GetShader());
			var texture = new Texture2D(512, 512, TextureFormat.RGBA32, true) { name = "BallDebugTexture" };
			texture.LoadImage(Resource.BallDebug.Data);
			material.SetTexture(MainTex, texture);
			material.SetFloat(Metallic, 1f);
			material.SetFloat(Glossiness, 1f);
			return material;
		}
	}
}
