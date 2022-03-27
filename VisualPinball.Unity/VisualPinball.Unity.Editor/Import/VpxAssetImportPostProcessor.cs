// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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


//TODO:		It would be benefitial to extend this out to a more fully featured asset pipeline
//			in the future.  See: https://tech.innogames.com/building-a-custom-asset-pipeline-for-a-unity-project/
// -pandeli 


using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq; 

namespace VisualPinball.Unity.Editor
{
	public class VpxAssetImportPostProcessor : AssetPostprocessor
	{
		private string[] normalIDs = new string[] { "_normal", "_nrm", "_nrml" };
		private string[] maskIDs = new string[] { "_mask", "_msk", "_madr", "_mads" };
		private string[] thickIDs = new string[] { "_thickness", "_thick", "_th" };
		private string[] tangentIDs = new string[] { "_tangent", "_tan", "_tng" };
		private string[] coatIDs = new string[] { "_coat", "_coatmask" };
		private string[] emitIDs = new string[] { "_emission", "_emit", "_em", "_emm" };
		

		void OnPreprocessTexture()
		{

			TextureImporter importer = assetImporter as TextureImporter;
			string textureName = PrepareName(assetPath);  //Prepare string for evaluation. 

			//Common settings to apply to all textures.  
			importer.mipmapEnabled = true;
			importer.streamingMipmaps = true;

			//Process normal map inputs and set normal map and compression flags.  
			if(normalIDs.Any(textureName.EndsWith))
			{
				importer.textureType = TextureImporterType.NormalMap;
				importer.sRGBTexture = false;
				importer.textureCompression = TextureImporterCompression.CompressedHQ;
				Debug.Log("Processed normal map: " + textureName);
			}

			//Process mask files to ensure they are linear.  
			if(maskIDs.Any(textureName.EndsWith))
			{
				importer.sRGBTexture = false;
				Debug.Log("Processed mask map: " + textureName);
			}

			//Emsure thickness maps are linear. 
			if(thickIDs.Any(textureName.EndsWith))
			{
				importer.sRGBTexture = false;
				Debug.Log("Processed thickness map: " + textureName);
			}

			//Ensure tangent maps are set to normal
			if(tangentIDs.Any(textureName.EndsWith))
			{
				importer.textureType = TextureImporterType.NormalMap;
				importer.sRGBTexture = false;
				Debug.Log("Processed tangent map: " + textureName);
			}

			//Ensure coat roughness maps are linear and single channel.  
			if(coatIDs.Any(textureName.EndsWith))
			{
				importer.textureType = TextureImporterType.SingleChannel;
				importer.sRGBTexture = false; 
				Debug.Log("Processed coat roughness map: " + textureName);
			}
			//Ensure emission maps are high quality to avoid compression artifacts.  
			if(emitIDs.Any(textureName.EndsWith))
			{
				importer.textureCompression = TextureImporterCompression.CompressedHQ;
				Debug.Log("Processed emission map: " + textureName);
			}



			
		}

		/// <summary>
		/// Prepares a texture asset name for sorting.  
		/// Removes MAP from name
		/// </summary>
		/// <param name="name">Input name from asset importer</param>
		/// <returns>A clean name for sorting</returns>
		private string PrepareName(string srcString)
		{
			string retName = "";
			//Supported extention names to cull.  
			string[] fileTypes = new string[] { ".png", ".tga", ".tif", ".tiff", ".psd", ".jpg", ".bmp", ".svg", ".hdr", ".exr", ".hdri"};
			
			retName = Path.GetFileName(assetPath);  //Remove unimportant path portion.  
			retName = srcString.ToLowerInvariant();  //Drop to lower ignoring cultural variants. 
			
			foreach(string ext in fileTypes)
			{
				if(retName.EndsWith(ext))
				{
					retName = retName.Substring(0, retName.LastIndexOf(ext));
					break; 
				}
			}

			if(retName.EndsWith("map"))
			{
				string m = "map"; 
				retName = retName.Substring(0, retName.LastIndexOf(m)); 
			}

			return retName; 
		}
		
	}

}


