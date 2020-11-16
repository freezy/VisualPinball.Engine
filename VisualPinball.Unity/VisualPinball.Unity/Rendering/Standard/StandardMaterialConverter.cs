// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Text;
using UnityEngine;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	public class StandardMaterialConverter : IMaterialConverter
	{
		#region Shader Properties

		private static readonly int BaseColor = Shader.PropertyToID("_Color");
		private static readonly int BaseColorMap = Shader.PropertyToID("_MainTex");
		private static readonly int NormalMap = Shader.PropertyToID("_BumpMap");
		private static readonly int Metallic = Shader.PropertyToID("_Metallic");
		private static readonly int Smoothness = Shader.PropertyToID("_Glossiness");
		private static readonly int Mode = Shader.PropertyToID("_Mode");
		private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
		private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
		private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");

		#endregion

		public Shader GetShader()
		{
			return Shader.Find("Standard");
		}

		private Shader GetShader(PbrMaterial vpxMaterial)
		{
			return vpxMaterial.VertexLerpWithUvEnabled
				? Shader.Find("Visual Pinball/Built-In/LerpVertex")
				: GetShader();
		}

		public static UnityEngine.Material GetDefaultMaterial(BlendMode blendMode)
		{
			switch (blendMode)
			{
				case BlendMode.Opaque:
					return UnityEngine.Resources.Load<UnityEngine.Material>("Materials/TableOpaque");
				case BlendMode.Cutout:
					return UnityEngine.Resources.Load<UnityEngine.Material>("Materials/TableCutout");
				case BlendMode.Translucent:
					return UnityEngine.Resources.Load<UnityEngine.Material>("Materials/TableTranslucent");
				default:
					throw new ArgumentOutOfRangeException("Undefined blend mode " + blendMode);
			}

		}

		public UnityEngine.Material CreateMaterial(PbrMaterial vpxMaterial, TableAuthoring table, Type objectType, StringBuilder debug = null)
		{
			UnityEngine.Material defaultMaterial = GetDefaultMaterial(vpxMaterial.MapBlendMode);

			var unityMaterial = new UnityEngine.Material(GetShader());
			unityMaterial.CopyPropertiesFromMaterial(defaultMaterial);
			unityMaterial.name = vpxMaterial.Id;

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


			if (vpxMaterial.MapBlendMode == BlendMode.Translucent)
			{
				col.a = Mathf.Min(1, Mathf.Max(0, vpxMaterial.Opacity));
			}
			unityMaterial.SetColor(BaseColor, col);

			// validate IsMetal. if true, set the metallic value.
			// found VPX authors setting metallic as well as translucent at the
			// same time, which does not render correctly in unity so we have
			// to check if this value is true and also if opacity <= 1.
			float metallicValue = 0f;
			if (vpxMaterial.IsMetal && (!vpxMaterial.IsOpacityActive || vpxMaterial.Opacity >= 1))
			{
				metallicValue = 1f;
				debug?.AppendLine("Metallic set to 1.");
			}

			unityMaterial.SetFloat(Metallic, metallicValue);

			// roughness / glossiness
			unityMaterial.SetFloat(Smoothness, vpxMaterial.Roughness);

			// map
			if (table != null && vpxMaterial.HasMap)
			{
				unityMaterial.SetTexture(BaseColorMap, table.GetTexture(vpxMaterial.Map.Name));
			}

			// normal map
			if (table != null && vpxMaterial.HasNormalMap)
			{
				unityMaterial.EnableKeyword("_NORMALMAP");
				unityMaterial.EnableKeyword("_NORMALMAP_TANGENT_SPACE");

				unityMaterial.SetTexture(NormalMap, table.GetTexture(vpxMaterial.NormalMap.Name));
			}

			return unityMaterial;
		}
	}
}
