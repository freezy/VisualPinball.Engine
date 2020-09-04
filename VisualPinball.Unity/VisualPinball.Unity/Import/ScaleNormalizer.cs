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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VisualPinball.Unity
{
	internal class ScaleNormalizer
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
