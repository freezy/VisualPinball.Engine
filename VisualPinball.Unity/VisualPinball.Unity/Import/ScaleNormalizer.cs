using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VisualPinball.Unity
{
	public class ScaleNormalizer
	{
		public static void Normalize(GameObject rootGameObject, float scale) {
			var transforms = FindObjectsOfTypeAll<Transform>();
			foreach (var transform in transforms) {
				var positionFix = transform.localPosition;
				positionFix *= scale;
				transform.localPosition = positionFix;
			}

			var mfs = FindObjectsOfTypeAll<MeshFilter>();
			foreach (var mf in mfs) {
				var trs = new Matrix4x4();

				//use the root scale to adjust the per item scale as well
				var scaleCurrent = mf.gameObject.transform.localScale * scale;

				trs.SetTRS(Vector3.zero, Quaternion.identity, scaleCurrent);
				var m = mf.sharedMesh;
				var vertices = m.vertices;
				for (var j = 0; j < vertices.Length; j++) {
					vertices[j] = trs.MultiplyPoint(vertices[j]);
				}
				m.vertices = vertices;
				mf.gameObject.transform.localScale = Vector3.one;

				m.RecalculateBounds();
			}
			rootGameObject.transform.localScale = Vector3.one;
		}

		private static List<T> FindObjectsOfTypeAll<T>() {
			var results = new List<T>();
			for (var i = 0; i < SceneManager.sceneCount; i++) {
				var scene = SceneManager.GetSceneAt(i);
				var allGameObjects = scene.GetRootGameObjects();
				foreach (var go in allGameObjects) {
					results.AddRange(go.GetComponentsInChildren<T>(true));
				}
			}
			return results;
		}
	}
}
