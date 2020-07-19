#region ReSharper
// ReSharper disable FieldCanBeMadeReadOnly.Global
#endregion

using System;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// A material, as seen in Visual Pinball's Material Manager.
	/// </summary>
	[Serializable]
	public class Material
	{
		public string Name;

		/// <summary>
		/// Wrap/rim lighting factor (0(off)..1(full))
		/// </summary>
		public float WrapLighting;

		/// <summary>
		/// Roughness seems to be mapped to the "specular" exponent.
		/// </summary>
		///
		/// <description>
		/// Comment when importing:
		///
		/// > normally a wavefront material specular exponent ranges from 0..1000.
		/// > but our shininess calculation differs from the way how e.g. Blender is calculating the specular exponent
		/// > starting from 0.5 and use only half of the exponent resolution to get a similar look
		///
		/// Then the roughness is converted like this:
		/// > mat->m_fRoughness = 0.5f + (tmp / 2000.0f);
		///
		/// When sending to the render device, the roughness is defined like that:
		/// > fRoughness = exp2f(10.0f * mat->m_fRoughness + 1.0f); // map from 0..1 to 2..2048
		/// </description>
		public float Roughness;

		/// <summary>
		/// Use image also for the glossy layer (0(no tinting at all)..1(use image)),
		/// stupid quantization because of legacy loading/saving
		/// </summary>
		public float GlossyImageLerp;

		/// <summary>
		/// Thickness for transparent materials (0(paper thin)..1(maximum)),
		/// stupid quantization because of legacy loading/saving
		/// </summary>
		public float Thickness;

		/// <summary>
		/// Edge weight/brightness for glossy and clearcoat (0(dark edges)..1(full fresnel))
		/// </summary>
		public float Edge;
		public float EdgeAlpha;
		public float Opacity;

		/// <summary>
		/// Can be overridden by texture on object itself
		/// </summary>
		public Color BaseColor;

		/// <summary>
		/// Specular of glossy layer
		/// </summary>
		public Color Glossiness;

		/// <summary>
		/// Specular of clearcoat layer
		/// </summary>
		public Color ClearCoat;

		/// <summary>
		/// Is a metal material or not
		/// </summary>
		public bool IsMetal;

		public bool IsOpacityActive;

		// physics
		public float Elasticity;
		public float ElasticityFalloff;
		public float Friction;
		public float ScatterAngle;

		internal readonly MaterialData MaterialData;
		internal PhysicsMaterialData PhysicsMaterialData;

		public Material()
		{
			MaterialData = new MaterialData();
			PhysicsMaterialData = new PhysicsMaterialData();
		}

		public Material(BinaryReader reader)
		{
			MaterialData = new MaterialData(reader);
			PhysicsMaterialData = new PhysicsMaterialData();

			Name = MaterialData.Name;
			BaseColor = new Color(MaterialData.BaseColor, ColorFormat.Bgr);
			Glossiness = new Color(MaterialData.Glossiness, ColorFormat.Bgr);
			ClearCoat = new Color(MaterialData.ClearCoat, ColorFormat.Bgr);
			WrapLighting = MaterialData.WrapLighting;
			Roughness = MaterialData.Roughness;
			GlossyImageLerp = 1f - BiffFloatAttribute.DequantizeUnsigned(8, MaterialData.GlossyImageLerp); //1.0f - dequantizeUnsigned<8>(mats[i].fGlossyImageLerp); //!! '1.0f -' to be compatible with previous table versions
			Thickness = MaterialData.Thickness == 0 ? 0.05f : BiffFloatAttribute.DequantizeUnsigned(8, MaterialData.Thickness); //!! 0 -> 0.05f to be compatible with previous table versions
			Edge = MaterialData.Edge;
			Opacity = MaterialData.Opacity;
			IsMetal = MaterialData.IsMetal > 0;
			IsOpacityActive = (MaterialData.OpacityActiveEdgeAlpha & 1) != 0;
			EdgeAlpha = BiffFloatAttribute.DequantizeUnsigned(7, MaterialData.OpacityActiveEdgeAlpha >> 1); //dequantizeUnsigned<7>(mats[i].bOpacityActiveEdgeAlpha >> 1);
		}

		public Material(string name) : this()
		{
			Name = name;
			BaseColor = new Color(255, 255, 255, 255);
			Glossiness = new Color(0, 0, 0, 255);
			ClearCoat = new Color(0, 0, 0, 255);

			UpdateData();
		}

		public Material Clone(string name = null)
		{
			var clone = new Material {
				Name = string.IsNullOrEmpty(name) ? Name : name,
				WrapLighting = WrapLighting,
				Roughness = Roughness,
				GlossyImageLerp = GlossyImageLerp,
				Thickness = Thickness,
				Edge = Edge,
				EdgeAlpha = EdgeAlpha,
				Opacity = Opacity,
				BaseColor = BaseColor.Clone(),
				Glossiness = Glossiness.Clone(),
				ClearCoat = ClearCoat.Clone(),
				IsMetal = IsMetal,
				IsOpacityActive = IsOpacityActive,
				Elasticity = Elasticity,
				ElasticityFalloff = ElasticityFalloff,
				Friction = Friction,
				ScatterAngle = ScatterAngle,
			};
			clone.UpdateData();
			return clone;
		}

		public void UpdateData()
		{
			MaterialData.Name = Name;
			MaterialData.BaseColor = BaseColor.ToInt(ColorFormat.Bgr);
			MaterialData.Glossiness = Glossiness.ToInt(ColorFormat.Bgr);
			MaterialData.ClearCoat = ClearCoat.ToInt(ColorFormat.Bgr);
			MaterialData.WrapLighting = WrapLighting;
			MaterialData.Roughness = Roughness;
			MaterialData.GlossyImageLerp = (byte)BiffFloatAttribute.QuantizeUnsigned(8, MathF.Clamp(1f - GlossyImageLerp, 0f, 1f));
			MaterialData.Thickness =  (byte)BiffFloatAttribute.QuantizeUnsigned(8, MathF.Clamp(Thickness, 0f, 1f));
			MaterialData.Edge = Edge;
			MaterialData.Opacity = Opacity;
			MaterialData.IsMetal = IsMetal ? (byte)1 : (byte)0;
			MaterialData.OpacityActiveEdgeAlpha = IsOpacityActive ? (byte)1 : (byte)0;
			MaterialData.OpacityActiveEdgeAlpha |= (byte)(BiffFloatAttribute.QuantizeUnsigned(7, MathF.Clamp(EdgeAlpha, 0f, 1f)) << 1);

			PhysicsMaterialData.Name = Name;
			PhysicsMaterialData.Elasticity = Elasticity;
			PhysicsMaterialData.ElasticityFallOff = ElasticityFalloff;
			PhysicsMaterialData.Friction = Friction;
			PhysicsMaterialData.ScatterAngle = ScatterAngle;
		}

		public void UpdatePhysics(PhysicsMaterialData physMat)
		{
			PhysicsMaterialData = physMat;
			Elasticity = physMat.Elasticity;
			ElasticityFalloff = physMat.ElasticityFallOff;
			Friction = physMat.Friction;
			ScatterAngle = physMat.ScatterAngle;
		}
	}
}
