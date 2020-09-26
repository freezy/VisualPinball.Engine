using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// This base class for all our Editor Window centralize all common features our editor windows could use
	/// </summary>
	/// <remarks>
	/// Focus region : focusedEditor flag management taking into account SceneView selection (for camera manipulation)
	/// </remarks>
	public class BaseEditorWindow : EditorWindow
	{
		protected virtual void OnEnable()
		{
			SceneView.beforeSceneGui += CheckFocusedEditor;
		}

		protected virtual void OnDisable()
		{
			SceneView.beforeSceneGui -= CheckFocusedEditor;
		}

		#region Focus
		/// <summary>
		/// Tells if the implemented editor is the current or last one being focused
		/// </summary>
		protected bool _isCurrentOrLastFocusedEditor = false;
		/// <summary>
		/// Tells if we keep this editor as being focused if the scene view is the next focused editor
		/// </summary>
		protected bool _allowSceneViewFocus = true;

		protected virtual void OnEditorFocused() { }
		protected virtual bool ValidateFocusOnSceneView() { return _allowSceneViewFocus; }

		private void CheckFocusedEditor(SceneView view)
		{
			//Update the last focused editor window to know if it was this editor
			//Didn't do it using OnFocus & OnLostFocus because there are some weirdnesses at OnLostFocus when selecting a window in another panel than the one of this editor 
			if (_isCurrentOrLastFocusedEditor) {
				if (EditorWindow.focusedWindow != this) {
					if (EditorWindow.focusedWindow != SceneView.lastActiveSceneView || !ValidateFocusOnSceneView()) {
						_isCurrentOrLastFocusedEditor = false;
					}
				}
			} else {
				if (EditorWindow.focusedWindow == this) {
					_isCurrentOrLastFocusedEditor = true;
					OnEditorFocused();
				}
			}
		}

		#endregion
	}
}
