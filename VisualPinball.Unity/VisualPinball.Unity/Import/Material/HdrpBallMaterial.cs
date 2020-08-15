using UnityEngine;
using VisualPinball.Resources;

namespace VisualPinball.Unity
{
	public class HdrpBallMaterial : IBallMaterial
	{
		#region Shader Properties

		private static readonly int BaseColorMap = Shader.PropertyToID("_BaseColorMap");
		private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
		private static readonly int Metallic = Shader.PropertyToID("_Metallic");
		private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");

		#endregion

		public Shader GetShader()
		{
			return Shader.Find("HDRP/Lit");
		}

		public Material CreateMaterial()
		{
			var material = new Material(GetShader());
			var texture = new Texture2D(512, 512, TextureFormat.RGBA32, true) { name = "BallDebugTexture" };
			texture.LoadImage(Resource.BallDebug.Data);
			material.SetTexture(BaseColorMap, texture);
			material.SetColor(BaseColor, Color.white);
			material.SetFloat(Metallic, 0.85f);
			material.SetFloat(Smoothness, 0.75f);
			return material;
		}
	}
}
