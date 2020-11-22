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

using UnityEngine;
using UnityEditor; 
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System;

namespace VisualPinball.Unity.Editor
{
	#if UNITY_EDITOR
	[InitializeOnLoad]
	#endif
	public class SetupEditorHierarchy
	{
		private static string editorObjectName = "EditorScene";
		private static string editorCameraName = "EditorCamera";
		private static string editorPostName = "EditorPostProcess";
		private static string editorLightEnvName = "EditorLighting"; 


		/// <summary>
		/// Returns the editor camera object reference. 
		/// </summary>
		public static GameObject editorCamera { get; private set; }
		/// <summary>
		/// Returns the editor post process volume reference. 
		/// </summary>
		public static GameObject editorPost { get; private set; }

		/// <summary>
		/// Returns the editor root hierarchy object.  
		/// </summary>
		public static GameObject editorRootObject { get; private set; }

		/// <summary>
		/// Reruns the editor light environment. 
		/// </summary>
		public static GameObject editorLightEnvironment { get; private set; }

		static SetupEditorHierarchy()
		{
			Initialize();
		}

		private static void Initialize()
		{
			editorRootObject = FindEditorObject(editorObjectName);
			editorCamera = FindEditorObject(editorCameraName);
			editorPost = FindEditorObject(editorPostName);
			editorLightEnvironment = FindEditorObject(editorLightEnvName); 
			
		}

		private static GameObject FindEditorObject(string obj)
		{
			GameObject returnVal = null;	

			GameObject[] rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
			
			foreach(GameObject rootGameObject in rootGameObjects)
			{
				if(rootGameObject.name == obj)
				{
					returnVal = rootGameObject;
					break;
				}
				else
				{
					Transform[] children = rootGameObject.GetComponentsInChildren<Transform>();
					foreach(Transform trans in children)
					{
						GameObject go = trans.gameObject;
						if(go.name == obj)
						{
							returnVal = go;
							break; 
						}
					}
				}
			}

			return returnVal; 
		}

		
		/// <summary>
		/// Recreates the entire editor scene hierarchy and returns the GameObject reference to the root object. 
		/// </summary>
		/// <returns>The root GameObject reference.</returns>
		public static GameObject CreateEditorDefaults()
		{
			GameObject go = CreateEditorHierarchy();
			return go; 
		}

		
		private static GameObject CreateEditorHierarchy()
		{
			editorRootObject = FindEditorObject(editorObjectName);
		
			if(editorRootObject == null)
			{
				GameObject _editorRoot = new GameObject();
				Undo.RegisterCreatedObjectUndo(_editorRoot, "Create root");
				_editorRoot.name = editorObjectName;
				_editorRoot.transform.position = Vector3.zero;
				_editorRoot.transform.rotation = Quaternion.identity;
				EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
				editorRootObject = _editorRoot;
			}
			
			GameObject cc = CreateCamera(editorRootObject);
			Undo.RegisterCreatedObjectUndo(cc, "Create camera");

			GameObject cp = CreatePost(editorRootObject);
			Undo.RegisterCreatedObjectUndo(cp, "Create post");

			GameObject cle = CreateLightEnvironment(editorRootObject);
			Undo.RegisterCreatedObjectUndo(cle, "Create light environment");

			return editorRootObject;

		}



		/// <summary>
		/// Creates the default editor post process volume object.
		/// Returns null if unable to create. 
		/// </summary>
		/// <param name="editorRootObj">The root to attach to.</param>
		public static GameObject CreatePost(GameObject editorRootObj)
		{

			if(editorPost != null) return editorPost; 

			GameObject postProcess = null; 

			GameObject post = (GameObject)AssetDatabase.LoadAssetAtPath(AssetPaths.assetRoot + AssetPaths.prefabPath + AssetPaths.postPath, typeof(GameObject));
			if(post != null)
			{
				//Spawn the prefab in the scene. 
				GameObject newPost = (GameObject)PrefabUtility.InstantiatePrefab(post as GameObject);
				if(newPost)
				{
					newPost.transform.SetParent(editorRootObj.transform, true);
					postProcess = newPost;
					editorPost = newPost; 
				}

			}

			return postProcess; 
		}


		/// <summary>
		/// Creates the default editor camera object.  
		/// Returns null if unable to create. 
		/// </summary>
		/// <param name="editorRootObj">The root to attach to.</param>
		private static GameObject CreateCamera(GameObject editorRootObj)
		{
			if(editorCamera != null) return editorCamera; 


			GameObject editCam = null; 

			GameObject camera = (GameObject)AssetDatabase.LoadAssetAtPath(AssetPaths.assetRoot + AssetPaths.prefabPath + AssetPaths.cameraPath, typeof(GameObject));
			if(camera != null)
			{
				//Spawn the prefab in the scene. 
				
				GameObject newCamera = (GameObject)PrefabUtility.InstantiatePrefab(camera as GameObject);
				if(newCamera)
				{
					newCamera.transform.SetParent(editorRootObj.transform, true);
					editCam = newCamera; 
					editorCamera = newCamera;
				}

			}

			return editCam;
		}

		/// <summary>
		/// Creates the lighting environment for the editor. 
		/// </summary>
		/// <returns></returns>
		public static GameObject CreateLightEnvironment()
		{
			GameObject lightEnv = null;

			if(!editorRootObject) CreateEditorHierarchy(); 
			
			lightEnv = CreateLightEnvironment(editorRootObject);

			return lightEnv; 

		}

		/// <summary>
		/// Creates the default editor light environment. 
		/// </summary>
		/// <param name="editorRootObj">The root to attach to.</param>
		/// <returns>A reference to created light environment</returns>
		private static GameObject CreateLightEnvironment(GameObject editorRootObj)
		{
			//We already have a reference to the editor light environment, don't 
			if(editorLightEnvironment != null) return editorLightEnvironment; 

			GameObject lightEnvironment = null;
			GameObject lightEnv = (GameObject)AssetDatabase.LoadAssetAtPath(AssetPaths.assetRoot + AssetPaths.prefabPath + AssetPaths.lighEnvPath, typeof(GameObject));

			if(lightEnv)
			{
				//Spawn the prefab in the scene. 
				GameObject newVolume = (GameObject)PrefabUtility.InstantiatePrefab(lightEnv as GameObject);
				if(newVolume)
				{
					newVolume.transform.SetParent(editorRootObject.transform, false);
					lightEnvironment = newVolume;
					editorLightEnvironment = newVolume; 
				}
				
			}

			return lightEnvironment;
		}
	}
}
