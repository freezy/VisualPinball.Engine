using UnityEditor;
using VisualPinball.Unity.Editor.Utils;
using VisualPinball.Unity.VPT.Gate;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(GateBehavior))]
	public class GateInspector : ItemInspector
	{
		private GateBehavior _gate;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_gate = target as GateBehavior;
		}

		public override void OnInspectorGUI()
		{
			base.OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DataFieldUtils.ItemDataField("Visible", ref _gate.data.IsVisible, FinishEdit);
				DataFieldUtils.ItemDataField("Show Bracket", ref _gate.data.ShowBracket, FinishEdit);
				DataFieldUtils.ItemDataField("Reflection Enabled", ref _gate.data.IsReflectionEnabled, FinishEdit);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				DataFieldUtils.ItemDataField("", ref _gate.data.Center, FinishEdit);
				DataFieldUtils.ItemDataField("Length", ref _gate.data.Length, FinishEdit);
				DataFieldUtils.ItemDataField("Height", ref _gate.data.Height, FinishEdit);
				DataFieldUtils.ItemDataField("Rotation", ref _gate.data.Rotation, FinishEdit);
				DataFieldUtils.ItemDataField("Open Angle", ref _gate.data.AngleMax, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Close Angle", ref _gate.data.AngleMin, FinishEdit, ("dirtyMesh", false));
				SurfaceField("Surface", ref _gate.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				DataFieldUtils.ItemDataField("Elasticity", ref _gate.data.Elasticity, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Friction", ref _gate.data.Friction, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Damping", ref _gate.data.Damping, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Gravity Factor", ref _gate.data.GravityFactor, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Collidable", ref _gate.data.IsCollidable, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Two Way", ref _gate.data.TwoWay, FinishEdit, ("dirtyMesh", false));
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				DataFieldUtils.ItemDataField("Timer Enabled", ref _gate.data.IsTimerEnabled, FinishEdit, ("dirtyMesh", false));
				DataFieldUtils.ItemDataField("Timer Interval", ref _gate.data.TimerInterval, FinishEdit, ("dirtyMesh", false));
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
