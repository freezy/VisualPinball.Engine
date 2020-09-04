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

using System;
using System.Text;
using NLog;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Patcher.Matcher;
using Logger = NLog.Logger;
using Material = UnityEngine.Material;

namespace VisualPinball.Unity
{
	public static class MaterialExtensions
	{

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Material Converter instance for the current graphics pipeline
		/// </summary>
		public static IMaterialConverter MaterialConverter => CreateMaterialConverter();

		/// <summary>
		/// Create a material converter depending on the graphics pipeline
		/// </summary>
		/// <returns></returns>
		private static IMaterialConverter CreateMaterialConverter()
		{
			switch (RenderPipeline.Current)
			{
				case RenderPipelineType.BuiltIn:
					return new StandardMaterialConverter();
				case RenderPipelineType.Hdrp:
					return new HdrpMaterialConverter();
				case RenderPipelineType.Urp:
					return new UrpMaterialConverter();
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

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

			var unityMaterial = MaterialConverter.CreateMaterial(vpxMaterial, table, debug);

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
