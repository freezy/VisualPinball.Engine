using System.IO;
using VisualPinball.Engine.IO;

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
		public float Roughness = 0f;

		/// <summary>
		/// Use image also for the glossy layer (0(no tinting at all)..1(use image)),
		/// stupid quantization because of legacy loading/saving
		/// </summary>
		public float GlossyImageLerp = 1f;

		/// <summary>
		/// Thickness for transparent materials (0(paper thin)..1(maximum)),
		/// stupid quantization because of legacy loading/saving
		/// </summary>
		public float Thickness = 0.05f;

		/// <summary>
		/// Edge weight/brightness for glossy and clearcoat (0(dark edges)..1(full fresnel))
		/// </summary>
		public float Edge = 1.0f;
		public float EdgeAlpha = 1.0f;
		public float Opacity = 1.0f;

		/// <summary>
		/// Can be overridden by texture on object itself
		/// </summary>
		public int BaseColor = 0xB469FF;

		/// <summary>
		/// Specular of glossy layer
		/// </summary>
		public float Glossiness = 0.0f;

		/// <summary>
		/// Specular of clearcoat layer
		/// </summary>
		public float ClearCoat = 0.0f;

		/// <summary>
		/// Is a metal material or not
		/// </summary>
		public bool IsMetal = false;

		public bool IsOpacityActive = false;

		// these are a additional props
		public int EmissiveColor;
		public int EmissiveIntensity = 0;
		public Texture EmissiveMap;

		// physics
		public float Elasticity = 0.0f;
		public float ElasticityFalloff = 0.0f;
		public float Friction = 0.0f;
		public float ScatterAngle = 0.0f;

		public Material(BinaryReader reader)
		{
			var saveMaterial = new SaveMaterial(reader);
			Name = saveMaterial.Name;
			BaseColor = BiffUtil.BgrToRgb(saveMaterial.BaseColor);
			Glossiness = BiffUtil.BgrToRgb(saveMaterial.Glossiness);
			ClearCoat = BiffUtil.BgrToRgb(saveMaterial.ClearCoat);
			WrapLighting = saveMaterial.WrapLighting;
			Roughness = saveMaterial.Roughness;
			GlossyImageLerp = 0; //1.0f - dequantizeUnsigned<8>(mats[i].fGlossyImageLerp); //!! '1.0f -' to be compatible with previous table versions
			Thickness = 0; //(mats[i].fThickness == 0) ? 0.05f : dequantizeUnsigned<8>(mats[i].fThickness); //!! 0 -> 0.05f to be compatible with previous table versions
			Edge = saveMaterial.Edge;
			Opacity = saveMaterial.Opacity;
			IsMetal = saveMaterial.IsMetal;
			IsOpacityActive = (saveMaterial.OpacityActiveEdgeAlpha & 1) != 0;
			EdgeAlpha = 0; //dequantizeUnsigned<7>(mats[i].bOpacityActiveEdgeAlpha >> 1);
		}

		public void UpdatePhysics(SavePhysicsMaterial savePhysMat) {
			Elasticity = savePhysMat.Elasticity;
			ElasticityFalloff = savePhysMat.ElasticityFallOff;
			Friction = savePhysMat.Friction;
			ScatterAngle = savePhysMat.ScatterAngle;
		}
	}

	/// <summary>
	/// This is the version of the material that is saved to the VPX file.
	/// </summary>
	internal class SaveMaterial
	{
		public const int Size = 76;

		public string Name;

		/// <summary>
		/// can be overriden by texture on object itself
		/// </summary>
		public int BaseColor;

		/// <summary>
		/// specular of glossy layer
		/// </summary>
		public int Glossiness;

		/// <summary>
		/// specular of clearcoat layer
		/// </summary>
		public int ClearCoat;

		/// <summary>
		/// wrap/rim lighting factor (0(off)..1(full))
		/// </summary>
		public float WrapLighting;

		/// <summary>
		/// is a metal material or not
		/// </summary>
		public bool IsMetal;

		/// <summary>
		/// roughness of glossy layer (0(diffuse)..1(specular))
		/// </summary>
		public float Roughness;

		/// <summary>
		/// use image also for the glossy layer (0(no tinting at all)..1(use image)), stupid quantization because of legacy loading/saving
		/// </summary>
		public int GlossyImageLerp;

		/// <summary>
		/// edge weight/brightness for glossy and clearcoat (0(dark edges)..1(full fresnel))
		/// </summary>
		public float Edge;

		/// <summary>
		/// thickness for transparent materials (0(paper thin)..1(maximum)), stupid quantization because of legacy loading/saving
		/// </summary>
		public int Thickness;

		/// <summary>
		/// opacity (0..1)
		/// </summary>
		public float Opacity;
		public int OpacityActiveEdgeAlpha;

		public SaveMaterial(BinaryReader reader)
		{
			var startPos = reader.BaseStream.Position;
			Name = BiffUtil.ReadNullTerminatedString(reader, 32);
			BaseColor = reader.ReadInt32();
			Glossiness = reader.ReadInt32();
			ClearCoat = reader.ReadInt32();
			WrapLighting = reader.ReadSingle();
			IsMetal = reader.ReadSByte() > 0;
			Roughness = reader.ReadSingle();
			GlossyImageLerp = reader.ReadInt32();
			Edge = reader.ReadSingle();
			Thickness = reader.ReadInt32();
			Opacity = reader.ReadSingle();
			OpacityActiveEdgeAlpha = reader.ReadInt32();

			var remainingSize = Size - (reader.BaseStream.Position - startPos);
			if (remainingSize > 0) {
				reader.BaseStream.Seek(remainingSize, SeekOrigin.Current);
			}
		}
	}

	/// <summary>
	/// That's the physics-related part of the material that is saved to the
	/// VPX file.
	/// </summary>
	public class SavePhysicsMaterial {

		public const int Size = 48;

		public string Name;
		public float Elasticity;
		public float ElasticityFallOff;
		public float Friction;
		public float ScatterAngle;

		public SavePhysicsMaterial(BinaryReader reader) {
			var startPos = reader.BaseStream.Position;
			Name = BiffUtil.ReadNullTerminatedString(reader, 32);
			Elasticity = reader.ReadSingle();
			ElasticityFallOff = reader.ReadSingle();
			Friction = reader.ReadSingle();
			ScatterAngle = reader.ReadSingle();
			var remainingSize = Size - (reader.BaseStream.Position - startPos);
			if (remainingSize > 0) {
				reader.BaseStream.Seek(remainingSize, SeekOrigin.Current);
			}
		}
	}
}
