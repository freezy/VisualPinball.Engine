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

// ReSharper disable StringLiteralTypo

using System;
using System.Text;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;
using Material = UnityEngine.Material;

namespace VisualPinball.Unity
{
	public static class MaterialExtensions
	{
		public static Material ToUnityMaterial(this PbrMaterial vpxMaterial, IMaterialProvider materialProvider, ITextureProvider textureProvider, Type objectType, StringBuilder debug = null)
		{
			if (materialProvider.HasMaterial(vpxMaterial.Id)) {
				return materialProvider.GetMaterial(vpxMaterial.Id);
			}

			var unityMaterial = RenderPipeline.Current.MaterialConverter.CreateMaterial(vpxMaterial, textureProvider, objectType, debug);

			materialProvider.SaveMaterial(vpxMaterial, unityMaterial);

			return unityMaterial;
		}

		public static string GetUnityFilename(this PbrMaterial vpMat, string folderName)
		{
			return $"{folderName}/{vpMat.Id.ToFilename()}.mat";
		}

	}
}
