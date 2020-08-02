﻿using UnityEngine;
using VisualPinball.Engine.Resources;

namespace VisualPinball.Unity.Import.Material
{
	public class HdrpBallMaterial : IBallMaterial
	{
		private readonly int BaseColorMap = Shader.PropertyToID("_BaseColorMap");
		private readonly int BaseColor = Shader.PropertyToID("_BaseColor");
		private readonly int Metallic = Shader.PropertyToID("_Metallic");
		private readonly int Smoothness = Shader.PropertyToID("_Smoothness");

		public Shader GetShader()
		{
			return Shader.Find("HDRP/Lit");
		}

		public UnityEngine.Material CreateMaterial()
		{
			var material = new UnityEngine.Material(GetShader());
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
