using System;
using UnityEditor;

namespace VisualPinball.Unity.Editor.Physics
{
	public class PhysicsWindow : EditorWindow
	{
		public static PhysicsWindow Window;

		[MenuItem("Visual Pinball/Physics Debug", false, 105)]
		public static void ShowWindow()
		{
			Window = GetWindow<PhysicsWindow>("Physics Debug");
			Window.Show();
		}

		private void OnGUI()
		{
			var refresh = false;
			var showAabbs = EditorGUILayout.Toggle("Show Bounding Boxes", PhysicsDebug.ShowAabbs);
			refresh = refresh || showAabbs == PhysicsDebug.ShowAabbs;
			PhysicsDebug.ShowAabbs = showAabbs;


			if (refresh) {
				EditorWindow view = GetWindow<SceneView>();
				view.Repaint();
			}

		}
	}
}
