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

// ReSharper disable StringLiteralTypo

using System.Text;
using VisualPinball.Engine.VPT;
using Material = UnityEngine.Material;

namespace VisualPinball.Unity
{
	public static class MaterialExtensions
	{
		public static Material ToUnityMaterial(this PbrMaterial vpxMaterial, TableAuthoring table, StringBuilder debug = null)
		{
			if (table != null)
			{
				var existingMat = table.GetMaterial(vpxMaterial);
				if (existingMat != null)
				{
					return existingMat;
				}
			}

			var unityMaterial = RenderPipeline.Current.MaterialConverter.CreateMaterial(vpxMaterial, table, debug);

			if (table != null)
			{
				table.AddMaterial(vpxMaterial, unityMaterial);
			}

			return unityMaterial;
		}

		public static string GetUnityFilename(this PbrMaterial vpMat, string folderName)
		{
			return $"{folderName}/{vpMat.Id}.mat";
		}

	}
}
