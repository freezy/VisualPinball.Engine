#region ReSharper
// ReSharper disable FieldCanBeMadeReadOnly.Global
#endregion

using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// A material, as seen in Visual Pinball's Material Manager.
	/// </summary>
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

		public Material(BinaryReader reader)
		{
			var saveMaterial = new MaterialData(reader);
			Name = saveMaterial.Name;
			BaseColor = new Color(saveMaterial.BaseColor, ColorFormat.Bgr);
			Glossiness = new Color(saveMaterial.Glossiness, ColorFormat.Bgr);
			ClearCoat = new Color(saveMaterial.ClearCoat, ColorFormat.Bgr);
			WrapLighting = saveMaterial.WrapLighting;
			Roughness = saveMaterial.Roughness;
			GlossyImageLerp = 1f - BiffFloatAttribute.DequantizeUnsigned(8, saveMaterial.GlossyImageLerp); //1.0f - dequantizeUnsigned<8>(mats[i].fGlossyImageLerp); //!! '1.0f -' to be compatible with previous table versions
			Thickness = saveMaterial.Thickness == 0 ? 0.05f : BiffFloatAttribute.DequantizeUnsigned(8, saveMaterial.Thickness); //!! 0 -> 0.05f to be compatible with previous table versions
			Edge = saveMaterial.Edge;
			Opacity = saveMaterial.Opacity;
			IsMetal = saveMaterial.IsMetal > 0;
			IsOpacityActive = (saveMaterial.OpacityActiveEdgeAlpha & 1) != 0;
			EdgeAlpha = BiffFloatAttribute.DequantizeUnsigned(7, saveMaterial.OpacityActiveEdgeAlpha >> 1); //dequantizeUnsigned<7>(mats[i].bOpacityActiveEdgeAlpha >> 1);
		}

		public Material(string name)
		{
			Name = name;
		}

		public void UpdatePhysics(PhysicsMaterialData physMat) {
			Elasticity = physMat.Elasticity;
			ElasticityFalloff = physMat.ElasticityFallOff;
			Friction = physMat.Friction;
			ScatterAngle = physMat.ScatterAngle;
		}
	}
}
