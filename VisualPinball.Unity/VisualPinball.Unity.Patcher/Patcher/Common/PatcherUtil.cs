using NLog;
using UnityEngine;
using RenderPipeline = VisualPinball.Unity.Patcher.Matcher.RenderPipeline;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Patcher
{
	/// <summary>
	/// Common methods for patching a table during import.
	/// </summary>
	static class PatcherUtil
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Hide the gameobject.
		/// </summary>
		/// <param name="gameObject"></param>
		public static void Hide(GameObject gameObject)
		{
			gameObject.SetActive(false);
		}

		/// <summary>
		/// Set a new parent for the given child while keeping the position and rotation.
		/// </summary>
		/// <param name="child"></param>
		/// <param name="parent"></param>
		public static void Reparent(GameObject child, GameObject parent)
		{
			var rot = child.transform.rotation;
			var pos = child.transform.position;

			// re-parent the child
			child.transform.SetParent(parent.transform, false);

			child.transform.rotation = rot;
			child.transform.position = pos;
		}
	}
}
