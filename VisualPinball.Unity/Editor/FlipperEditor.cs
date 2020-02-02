using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public class MyWindow : EditorWindow
	{
		string myString = "Hello World";
		bool groupEnabled;
		bool myBool = true;
		float myFloat = 1.23f;

		// Add menu item named "My Window" to the Window menu
		[MenuItem("Window/My Window")]
		public static void ShowWindow()
		{
			//Show existing window instance. If one doesn't exist, make one.
			EditorWindow.GetWindow(typeof(MyWindow));
		}

		void OnGUI()
		{
			GUILayout.Label ("Base Settings", EditorStyles.boldLabel);
			myString = EditorGUILayout.TextField ("Text Field", myString);

			groupEnabled = EditorGUILayout.BeginToggleGroup ("Optional Settings", groupEnabled);
			myBool = EditorGUILayout.Toggle ("Toggle", myBool);
			myFloat = EditorGUILayout.Slider ("Slider", myFloat, -3, 3);
			EditorGUILayout.EndToggleGroup ();
		}
	}
}
