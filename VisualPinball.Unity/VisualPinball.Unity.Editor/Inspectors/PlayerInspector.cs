using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(Player))]
	[CanEditMultipleObjects]
	public class PlayerInspector : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			if (EditorApplication.isPlaying) {
				if (GUILayout.Button("Spawn Ball")) {
					var player = (Player) target;
					player.CreateBall(new DebugBallCreator());
				}
			}
		}
	}
}
