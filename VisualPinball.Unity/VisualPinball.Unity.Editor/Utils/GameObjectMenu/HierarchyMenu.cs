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


using UnityEditor;
using UnityEngine;

/*  Saving for future expansion to VPT objects. 
using Light = VisualPinball.Engine.VPT.Light.Light;
using Texture = UnityEngine.Texture;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Trigger;
*/

using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using VisualPinball.Unity.Editor.Utils;


/*
#if USING_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif
*/


namespace VisualPinball.Unity.Editor
{

	[ExecuteInEditMode]
	public static class HierarchyMenu
	{
		private const string editorObjectName = "EditorScene";
		private static GameObject editorRootObject;
		

		[MenuItem("GameObject/Visual Pinball/Default Editor Environment", false, 12)]
		static void CreateEditorEnvironment()
		{
			SetupEditorHierarchy.CreateEditorDefaults();

		}


		[MenuItem("GameObject/Visual Pinball/Blueprint Projector", false, 12)]
		static void CreateBlueprintProjector()
		{
			
			//TODO: Move post-instantiation logic to BP authoring component.  Extend to make it simpler to swap projections. 

			GameObject decalProjector = (GameObject)AssetDatabase.LoadAssetAtPath(AssetPaths.assetRoot + AssetPaths.prefabPath + AssetPaths.blueprintPath, typeof(GameObject));

			if(decalProjector != null)
			{
				//Spawn the prefab in the scene. 
				GameObject newBPProjector = (GameObject)PrefabUtility.InstantiatePrefab(decalProjector as GameObject);
				newBPProjector.transform.localScale = new Vector3(1, 1, 1);
				Undo.RegisterCreatedObjectUndo(newBPProjector, "Blueprint Decal"); 

				//Fetch the active table component.  
				TableAuthoring tac = TableManager.GetActiveTable(); 

				PlayfieldAuthoring pac = null; 

				//Get the playfield authoring component reference. 
				if(tac)	pac = tac.GetComponentInChildren<PlayfieldAuthoring>();

				//Set the playfield as the parent.  
				//if(pac) newBPProjector.transform.SetParent(pac.transform);
				if(!SetupEditorHierarchy.editorRootObject)
				{
					SetupEditorHierarchy.CreateEditorDefaults();
				}

				newBPProjector.transform.SetParent(SetupEditorHierarchy.editorRootObject.transform, true);
				
				Vector3 extents = pac.gameObject.GetComponent<MeshRenderer>().bounds.extents;
				Vector3 center = pac.gameObject.GetComponent<MeshRenderer>().bounds.center;

				newBPProjector.transform.position = center + new Vector3(0, 1, 0);
				
				//TODO: Find way of including reference to this without including DLL.
				/*
				#if USING_HDRP
				newBPProjector.GetComponent<DecalProjector>().size = new Vector3(extents.x, extents.z, 1) * 2.0f;
				#endif
				*/


				//if(newBPProjector && editorRootObject) newBPProjector.transform.SetParent(editorRootObject.transform, true);
				
				EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
			}
			
		}
	}
}
