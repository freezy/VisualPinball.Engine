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

using System.Text;
using VisualPinball.Engine.VPT;
using Material = UnityEngine.Material;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Common interface for material conversion with the various render pipelines
	/// </summary>
	public interface IMaterialConverter
	{
		/// <summary>
		/// Loads the material used for the dot matrix display.
		/// </summary>
		Material DotMatrixDisplay { get; }

		/// <summary>
		/// Loads the material for the segment display.
		/// </summary>
		Material SegmentDisplay { get; }

		/// <summary>
		/// Create a material for the currently detected graphics pipeline.
		/// </summary>
		/// <param name="vpxMaterial"></param>
		/// <param name="textureProvider"></param>
		/// <param name="debug"></param>
		/// <returns></returns>
		Material CreateMaterial(PbrMaterial vpxMaterial, ITextureProvider textureProvider, StringBuilder debug = null);

		/// <summary>
		/// Takes a Unity material and applies the VPX properties minus textures on it.
		/// </summary>
		///
		/// <remarks>
		/// This is used for materials with built-in textures, such as bumpers. We basically
		/// take the prefab and override the non-texture props from the import.
		/// </remarks>
		/// <param name="vpxMaterial">VPX material</param>
		/// <param name="unityTextureMaterial">Unity material</param>
		/// <returns>Merged material</returns>
		Material MergeMaterials(PbrMaterial vpxMaterial, Material unityTextureMaterial);

		void SetDiffusionProfile(Material material, DiffusionProfileTemplate template);

		void SetMaterialType(Material material, MaterialType materialType);

		int NormalMapProperty { get; }
	}

	public enum DiffusionProfileTemplate
	{
		Plastics
	}

	public enum MaterialType
	{
		Standard,
		SubsurfaceScattering,
		Anisotropy,
		Iridescence,
		SpecularColor,
		Translucent
	}
}
