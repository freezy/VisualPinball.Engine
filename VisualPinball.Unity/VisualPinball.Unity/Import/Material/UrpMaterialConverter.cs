using System;
using System.Text;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Import.Material
{
	public class UrpMaterialConverter : IMaterialConverter
	{
		private readonly int Surface = Shader.PropertyToID("_Surface");
		private readonly int BaseColor = Shader.PropertyToID("_BaseColor");
		private readonly int BaseMap = Shader.PropertyToID("_BaseMap");
		private readonly int BumpMap = Shader.PropertyToID("_BumpMap");
		private readonly int Metallic = Shader.PropertyToID("_Metallic");
		private readonly int Smoothness = Shader.PropertyToID("_Smoothness");
		private readonly int Blend = Shader.PropertyToID("_Blend");
		private readonly int QueueOffset = Shader.PropertyToID("_QueueOffset");
		private readonly int AlphaClip = Shader.PropertyToID("_AlphaClip");
		private readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
		private readonly int DstBlend = Shader.PropertyToID("_DstBlend");
		private readonly int ZWrite = Shader.PropertyToID("_ZWrite");

		public Shader GetShader()
		{
			return Shader.Find("Universal Render Pipeline/Lit");
		}


		public UnityEngine.Material CreateMaterial(PbrMaterial vpxMaterial, TableBehavior table, StringBuilder debug = null)
		{
			var unityMaterial = new UnityEngine.Material(GetShader())
			{
				name = vpxMaterial.Id
			};

			// apply some basic manipulations to the color. this just makes very
			// very white colors be clipped to 0.8204 aka 204/255 is 0.8
			// this is to give room to lighting values. so there is more modulation
			// of brighter colors when being lit without blow outs too soon.
			var col = vpxMaterial.Color.ToUnityColor();
			if (vpxMaterial.Color.IsGray() && col.grayscale > 0.8)
			{
				debug?.AppendLine("Color manipulation performed, brightness reduced.");
				col.r = col.g = col.b = 0.8f;
			}
			unityMaterial.SetColor(BaseColor, col);

			// validate IsMetal. if true, set the metallic value.
			// found VPX authors setting metallic as well as translucent at the
			// same time, which does not render correctly in unity so we have
			// to check if this value is true and also if opacity <= 1.
			if (vpxMaterial.IsMetal && (!vpxMaterial.IsOpacityActive || vpxMaterial.Opacity >= 1))
			{
				unityMaterial.SetFloat(Metallic, 1f);
				debug?.AppendLine("Metallic set to 1.");
			}

			// roughness / glossiness
			unityMaterial.SetFloat(Smoothness, vpxMaterial.Roughness);

			// blend mode
			ApplyBlendMode(unityMaterial, vpxMaterial.MapBlendMode);
			if (vpxMaterial.MapBlendMode == Engine.VPT.BlendMode.Translucent)
			{
				col.a = Mathf.Min(1, Mathf.Max(0, vpxMaterial.Opacity));
				unityMaterial.SetColor(BaseColor, col);
			}

			// map
			if (table != null && vpxMaterial.HasMap)
			{
				unityMaterial.SetTexture(
					BaseMap,
					table.GetTexture(vpxMaterial.Map.Name)
				);
			}

			// normal map
			if (table != null && vpxMaterial.HasNormalMap)
			{
				unityMaterial.EnableKeyword("_NORMALMAP");

				unityMaterial.SetTexture(
					BumpMap,
					table.GetTexture(vpxMaterial.NormalMap.Name)
				);
			}

			return unityMaterial;
		}

		private void ApplyBlendMode(UnityEngine.Material unityMaterial, BlendMode blendMode)
		{
			switch (blendMode)
			{
				case Engine.VPT.BlendMode.Opaque:

					// required for the blend mode
					unityMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
					unityMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
					unityMaterial.SetInt("_ZWrite", 1);

					// properties
					unityMaterial.SetFloat(Surface, 0); // 0 = Opaque; 1 = Transparent

					// render queue
					unityMaterial.renderQueue = -1;

					break;

				case Engine.VPT.BlendMode.Cutout:

					// keywords
					unityMaterial.EnableKeyword("_ALPHATEST_ON");

					// required for blend mode
					unityMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
					unityMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
					unityMaterial.SetInt("_ZWrite", 1);

					// properties
					unityMaterial.SetFloat(Surface, 0); // 0 = Opaque; 1 = Transparent
					unityMaterial.SetInt(AlphaClip, 1);

					// render queue
					unityMaterial.renderQueue = 2450;

					break;

				case Engine.VPT.BlendMode.Translucent:

					// keywords
					unityMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");

					// required for blend mode
					unityMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
					unityMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
					unityMaterial.SetInt("_ZWrite", 0);

					// properties
					unityMaterial.SetFloat(Surface, 1); // 0 = Opaque; 1 = Transparent
					unityMaterial.SetFloat(Blend, 1); // 0 = Alpha, 1 = Premultiply, 2 = Additive, 3 = Multiply

					// render queue
					int transparentRenderQueueBase = 3000;
					int transparentSortingPriority = 0;
					unityMaterial.SetInt(QueueOffset, transparentSortingPriority);
					unityMaterial.renderQueue = transparentRenderQueueBase + transparentSortingPriority;

					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}
}
