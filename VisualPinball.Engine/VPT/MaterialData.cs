// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

// ReSharper disable FieldCanBeMadeReadOnly.Global

using System;
using System.IO;
using VisualPinball.Engine.IO;

namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// This is the version of the material that is saved to the VPX file (originally "SaveMaterial")
	/// </summary>
	internal class MaterialData
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
		/// specular of clear coat layer
		/// </summary>
		public int ClearCoat;

		/// <summary>
		/// wrap/rim lighting factor (0(off)..1(full))
		/// </summary>
		public float WrapLighting;

		/// <summary>
		/// is a metal material or not
		/// </summary>
		public byte IsMetal;

		/// <summary>
		/// roughness of glossy layer (0(diffuse)..1(specular))
		/// </summary>
		public float Roughness;

		/// <summary>
		/// use image also for the glossy layer (0(no tinting at all)..1(use image)), stupid quantization because of legacy loading/saving
		/// </summary>
		public byte GlossyImageLerp;

		/// <summary>
		/// edge weight/brightness for glossy and clear coat (0(dark edges)..1(full fresnel))
		/// </summary>
		public float Edge;

		/// <summary>
		/// thickness for transparent materials (0(paper thin)..1(maximum)), stupid quantization because of legacy loading/saving
		/// </summary>
		public byte Thickness;

		/// <summary>
		/// opacity (0..1)
		/// </summary>
		public float Opacity;

		public byte OpacityActiveEdgeAlpha;

		public MaterialData()
		{
		}

		public MaterialData(BinaryReader reader)
		{
			var startPos = reader.BaseStream.Position;
			Name = BiffUtil.ReadNullTerminatedString(reader, 32);
			BaseColor = reader.ReadInt32();
			Glossiness = reader.ReadInt32();
			ClearCoat = reader.ReadInt32();
			WrapLighting = reader.ReadSingle();
			IsMetal = reader.ReadByte();
			reader.BaseStream.Seek(3, SeekOrigin.Current);
			Roughness = reader.ReadSingle();
			GlossyImageLerp = reader.ReadByte();
			reader.BaseStream.Seek(3, SeekOrigin.Current);
			Edge = reader.ReadSingle();
			Thickness = reader.ReadByte();
			reader.BaseStream.Seek(3, SeekOrigin.Current);
			Opacity = reader.ReadSingle();
			OpacityActiveEdgeAlpha = reader.ReadByte();
			reader.BaseStream.Seek(3, SeekOrigin.Current);

			var remainingSize = Size - (reader.BaseStream.Position - startPos);
			if (remainingSize != 0) {
				throw new InvalidOperationException("There are still " + remainingSize + " bytes left to read.");
			}
		}

		public void Write(BinaryWriter writer)
		{
			writer.Write(BiffUtil.GetNullTerminatedString(Name, 32));
			writer.Write(BaseColor);
			writer.Write(Glossiness);
			writer.Write(ClearCoat);
			writer.Write(WrapLighting);
			writer.Write(IsMetal);
			writer.Write((byte)0x0);
			writer.Write((byte)0x0);
			writer.Write((byte)0x0);
			writer.Write(Roughness);
			writer.Write(GlossyImageLerp);
			writer.Write((byte)0x0);
			writer.Write((byte)0x0);
			writer.Write((byte)0x0);
			writer.Write(Edge);
			writer.Write(Thickness);
			writer.Write((byte)0x0);
			writer.Write((byte)0x0);
			writer.Write((byte)0x0);
			writer.Write(Opacity);
			writer.Write(OpacityActiveEdgeAlpha);
			writer.Write((byte)0x0);
			writer.Write((byte)0x0);
			writer.Write((byte)0x0);
		}
	}

	/// <summary>
	/// That's the physics-related part of the material that is saved to the
	/// VPX file (originally "SavePhysicsMaterial")
	/// </summary>
	public class PhysicsMaterialData {

		public const int Size = 48;

		public string Name;
		public float Elasticity;
		public float ElasticityFallOff;
		public float Friction;
		public float ScatterAngle;

		public PhysicsMaterialData()
		{
		}

		public PhysicsMaterialData(BinaryReader reader) {
			var startPos = reader.BaseStream.Position;
			Name = BiffUtil.ReadNullTerminatedString(reader, 32);
			Elasticity = reader.ReadSingle();
			ElasticityFallOff = reader.ReadSingle();
			Friction = reader.ReadSingle();
			ScatterAngle = reader.ReadSingle();
			var remainingSize = Size - (reader.BaseStream.Position - startPos);
			if (remainingSize > 0) {
				throw new InvalidOperationException("There are still " + remainingSize + " bytes left to read.");
				//reader.BaseStream.Seek(remainingSize, SeekOrigin.Current);
			}
		}

		public void Write(BinaryWriter writer)
		{
			writer.Write(BiffUtil.GetNullTerminatedString(Name, 32));
			writer.Write(Elasticity);
			writer.Write(ElasticityFallOff);
			writer.Write(Friction);
			writer.Write(ScatterAngle);
		}
	}
}
