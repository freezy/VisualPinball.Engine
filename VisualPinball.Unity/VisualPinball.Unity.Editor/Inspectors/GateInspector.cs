using UnityEditor;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(GateAuthoring))]
	public class GateInspector : ItemInspector
	{
		private GateAuthoring _gate;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		private static string[] _gateTypeStrings = { "Wire: 'W'", "Wire: Rectangle", "Plate", "Long Plate" };
		private static int[] _gateTypeValues = { GateType.GateWireW, GateType.GateWireRectangle, GateType.GatePlate, GateType.GateLongPlate };

		protected override void OnEnable()
		{
			base.OnEnable();
			_gate = target as GateAuthoring;
		}

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DropDownField("Type", ref _gate.data.GateType, _gateTypeStrings, _gateTypeValues);
				ItemDataField("Visible", ref _gate.data.IsVisible);
				ItemDataField("Show Bracket", ref _gate.data.ShowBracket);
				MaterialField("Material", ref _gate.data.Material);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref _gate.data.Center);
				ItemDataField("Length", ref _gate.data.Length);
				ItemDataField("Height", ref _gate.data.Height);
				ItemDataField("Rotation", ref _gate.data.Rotation);
				ItemDataField("Open Angle", ref _gate.data.AngleMax, dirtyMesh: false);
				ItemDataField("Close Angle", ref _gate.data.AngleMin, dirtyMesh: false);
				SurfaceField("Surface", ref _gate.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				ItemDataField("Elasticity", ref _gate.data.Elasticity, dirtyMesh: false);
				ItemDataField("Friction", ref _gate.data.Friction, dirtyMesh: false);
				ItemDataField("Damping", ref _gate.data.Damping, dirtyMesh: false);
				ItemDataField("Gravity Factor", ref _gate.data.GravityFactor, dirtyMesh: false);
				ItemDataField("Collidable", ref _gate.data.IsCollidable, dirtyMesh: false);
				ItemDataField("Two Way", ref _gate.data.TwoWay, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _gate.data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _gate.data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
