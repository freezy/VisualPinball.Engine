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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Unity.Patcher.Matcher; 

//TODO:  This doesn't seem like a particularly elegant way of doing this. 
//		 Please let me know if there is a smarter way.  -Pandeli 

namespace VisualPinball.Unity.Editor
{
    /// <summary>
    /// Communal storage for Asset Paths
    /// Directory references always contain a trailing / 
    /// </summary>
    public class AssetPaths
    {
        //TODO: Store this in a file instead of hard coding it here.   
         

        /// <summary>
        /// Root package folder for the shared asset library.  
        /// </summary>
        public const string root = "Packages/org.visualpinball.assetlibrary/VisualPinball.SharedLibrary.Resources/"; 
        /// <summary>
        /// Root folder for the Assets.  
        /// </summary>
        public const string assetRoot = root + "Assets/";
        /// <summary>
        /// HDRI Environments folder path. 
        /// </summary>
        public const string HDRIEnvs = assetRoot + "Art/Textures/HDR"; //Directory loading requires no trailing / 
        /// <summary>
        /// EditorGUI icons folder path. 
        /// </summary>
        public const string iconPath = assetRoot + "EditorResources/Icons/EditorGUI/";

        //public const string hdrp = "HDRP/";
        //public const string urp = "URP/";

        //Prefabs.  Should follow assetRoot + (Render pipeline) + ... 
        /// <summary>
        /// Light environment editor prefab. 
        /// </summary>
        public const string lighEnvPath = "Lighting/EditorLighting.prefab";
        /// <summary>
        /// Camera prefab. 
        /// </summary>
        public const string cameraPath = "Camera/EditorCamera.prefab";
        /// <summary>
        /// Post process prefab. 
        /// </summary>
        public const string postPath = "PostProcess/EditorPostProcess.prefab";
        /// <summary>
        /// Blueprint projector prefab.  
        /// </summary>
        public const string blueprintPath = "Tools/BlueprintProjector.prefab";

		#if USING_HDRP
		public const string prefabPath = "EditorResources/Prefabs/HDRP/";
		#endif

		#if USING_URP
		public const string prefabPath = "EditorResources/Prefabs/URP/";
		#endif

		
		// Saving this in case there are problems with the defines. 
		/*

		/// <summary>
		/// Path to the render pipeline specific prefabs folder.  Used with prefabs paths.
		/// Usage: assetRoot + prefabPath + prefab 
		/// </summary>
		public static string prefabPath = PrefabPath(); 

        /// <summary>
        /// The path to the render specific pipeline prefabs. 
        /// </summary>
        /// <returns>Path String</returns>
        private static string PrefabPath()
        {
            string returnVal = string.Empty;

			#if USING_HDRP
			returnVal = "EditorResources/Prefabs/HDRP/";
			#endif
			
			#if USING_URP
			returnVal = "EditorResources/Prefabs/URP/";
			#endif


            return returnVal; 
        }

		*/
    }
    
}
