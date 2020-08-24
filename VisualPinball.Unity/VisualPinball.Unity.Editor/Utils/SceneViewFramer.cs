using UnityEngine;
using UnityEditor;

namespace VisualPinball.Unity.Editor.Utils
{
	/// <summary>
	/// This class is a helper to Frame the scene view using objects boundaries
	/// </summary>
	public class SceneViewFramer
	{
		public static void FrameObjects(Object[] objects, bool includeChildren = true, bool instant = false)
		{
			Bounds bounds = new Bounds();
			foreach (var obj in objects) {
				if (obj is UnityEngine.GameObject gameObj) {
					var renders = includeChildren ? gameObj.GetComponentsInChildren<Renderer>() : gameObj.GetComponents<Renderer>();
					foreach (var render in renders) {
						bounds.Encapsulate(render.bounds);
					}
				}
			}

			if (bounds.extents != Vector3.zero) {
				SceneView.lastActiveSceneView.Frame(bounds, instant);
			}
		}
	}
}
