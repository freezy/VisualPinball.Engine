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

using System; 
using System.Collections;
using System.IO; 
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;



namespace VisualPinball.Unity.Editor 
{
	[InitializeOnLoad]
	public static class PopulateEditorLayout 
	{
		#region Private Variables
		private static string path = AssetPaths.layoutPath;
		private static List<string> layouts = new List<string>();
		private static List<string> layoutName = new List<string>();
		
		private enum GameViewSizeType
		{
			AspectRatio, FixedResolution
		}

		static object gameViewSizesInstance;

		#endregion

		static PopulateEditorLayout()
		{
			CollectAssets();
			string unityPrefs = UnityEditorInternal.InternalEditorUtility.unityPreferencesFolder;
			int i = 0; 
			foreach(string lpath in layouts)
			{
 
				int number = i + 1;

				//Rename default unity views that interrupt the VPE layout naming. 
				string theAnnoyingOne = unityPrefs + "/Layouts/default/2 by 3.wlt";
				string TAOCorrected = unityPrefs + "/Layouts/default/Unity 2 by 3.wlt";
				string theAnnoyingOne2 = unityPrefs + "/Layouts/default/4 Split.wlt";
				string TAO2Corrected = unityPrefs + "/Layouts/default/Unity 4 Split.wlt";

				//Rename
				if(File.Exists(theAnnoyingOne))
				{
					File.Copy(theAnnoyingOne, TAOCorrected);  
					File.Delete(theAnnoyingOne);
				}

				if(File.Exists(theAnnoyingOne2))
				{ 
					File.Copy(theAnnoyingOne2, TAO2Corrected);
					File.Delete(theAnnoyingOne2); 
				}

				//Copy the VPE preferences.  Overwrite the originals in case we change them. 
				//TODO: Provide a modal dialog prompt to for overwriting.   
				File.Copy(lpath, unityPrefs + "/Layouts/default/" + number.ToString() + ") " + layoutName[i] + ".wlt", true);
				
				i++;
			}

			//Refresh the menu. 
			UnityEditorInternal.InternalEditorUtility.ReloadWindowLayoutMenu();

		}

		[MenuItem("Visual Pinball/Add Game View Resolutions")]
		private static void PopulateEditorDisplaySizeMenu()
		{
			//This is a do once operation
			string unityPrefs = UnityEditorInternal.InternalEditorUtility.unityPreferencesFolder;
			AddCustomSize(GameViewSizeType.FixedResolution, GameViewSizeGroupType.Standalone, 1080, 1920, "Playfield 1080");
			AddCustomSize(GameViewSizeType.FixedResolution, GameViewSizeGroupType.Standalone, 2160, 3840, "Playfield 4K");

		}

		/// <summary>
		/// Collect all of the available layouts. 
		/// </summary>
		/// <returns></returns>
		private static bool CollectAssets()
		{
			bool returnValue = false;

			var assets = AssetDatabase.FindAssets("t:DefaultAsset", new[] { path });
			if(assets.Length > 0)
			{
				foreach(var guid in assets)
				{
					string pathToLayout = AssetDatabase.GUIDToAssetPath(guid);
					string name = AssetDatabase.LoadAssetAtPath(pathToLayout, typeof(DefaultAsset)).name; 
					layouts.Add(pathToLayout);
					layoutName.Add(name);

				}
				if(layouts.Count > 0) returnValue = true;

			}

			return returnValue;
		}

		private static void AddCustomSize(GameViewSizeType viewSizeType, GameViewSizeGroupType sizeGroupType, int width, int height, string text)
		{
			
			var asm = typeof(UnityEditor.Editor).Assembly;
			var sizesType = asm.GetType("UnityEditor.GameViewSizes");
			var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
			var instanceProp = singleType.GetProperty("instance");
			var getGroup = sizesType.GetMethod("GetGroup");
			var instance = instanceProp.GetValue(null, null);
			var group = getGroup.Invoke(instance, new object[] { (int)sizeGroupType });
			var addCustomSize = getGroup.ReturnType.GetMethod("AddCustomSize"); // or group.GetType().
			var gvsType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSize");
			var ctor = gvsType.GetConstructor(new Type[] { typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizeType"), typeof(int), typeof(int), typeof(string) }); var newSize = ctor.Invoke(new object[] { (int)viewSizeType, width, height, text });
			var newGvsType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
			gameViewSizesInstance = instanceProp.GetValue(null, null);

			addCustomSize.Invoke(group, new object[] { newSize });
			getGroup.Invoke(gameViewSizesInstance, new object[] { GameViewSizeGroupType.Standalone });

			//TODO: Make it save to disk so it doesn't go away on reload. 
			sizesType.GetMethod("SaveToHDD").Invoke(gameViewSizesInstance, null);

		}


	}

}
