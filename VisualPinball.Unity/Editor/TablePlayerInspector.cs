using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ball;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(TablePlayer))]
	public class TablePlayerInspector : UnityEditor.Editor
	{
		private TablePlayer _tablePlayer;

		private void OnEnable()
		{
			_tablePlayer = (TablePlayer) target;
		}

		public override void OnInspectorGUI()
		{
			_tablePlayer.ballMaterial = (Material)EditorGUILayout.ObjectField("Ball Material", _tablePlayer.ballMaterial, typeof(Material), false);

			if (GUILayout.Button("Spawn Ball")) {
				_tablePlayer.Player?.CreateBall(new TestBallCreator(600f, 200f, 0));
			}
		}
	}

	public class TestBallCreator : IBallCreationPosition
	{
		private readonly Vertex3D _pos;
		private readonly Vertex3D _vel;

		public TestBallCreator(float x, float y, float z)
		{
			_pos = new Vertex3D(x, y, z);
			_vel = new Vertex3D(0, 0, 0);
		}

		public Vertex3D GetBallCreationPosition(Table table) => _pos;

		public Vertex3D GetBallCreationVelocity(Table table) => _vel;

		public void OnBallCreated(PlayerPhysics physics, Ball ball)
		{
			// do nothing
		}
	}
}
