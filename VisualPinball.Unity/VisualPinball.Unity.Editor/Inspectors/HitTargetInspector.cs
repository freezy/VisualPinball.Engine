using UnityEditor;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(HitTargetAuthoring))]
	public class HitTargetInspector : ItemInspector
	{
		private HitTargetAuthoring _target;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		private static string[] _targetTypeStrings = {
			"Drop Target: Beveled",
			"Drop Target: Simple",
			"Drop Target: Flat Simple",
			"Hit Target: Rectangle",
			"Hit Target: Fat Rectangle",
			"Hit Target: Round",
			"Hit Target: Slim",
			"Hit Target: Fat Slim",
			"Hit Target: Fat Square",
		};
		private static int[] _targetTypeValues = {
			TargetType.DropTargetBeveled,
			TargetType.DropTargetSimple,
			TargetType.DropTargetFlatSimple,
			TargetType.HitTargetRectangle,
			TargetType.HitFatTargetRectangle,
			TargetType.HitTargetRound,
			TargetType.HitTargetSlim,
			TargetType.HitFatTargetSlim,
			TargetType.HitFatTargetSquare,
		};

		protected override void OnEnable()
		{
			base.OnEnable();
			_target = target as HitTargetAuthoring;
		}

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DropDownField("Type", ref _target.data.TargetType, _targetTypeStrings, _targetTypeValues);
				TextureField("Image", ref _target.data.Image);
				MaterialField("Material", ref _target.data.Material);
				ItemDataField("Drop Speed", ref _target.data.DropSpeed, dirtyMesh: false);
				ItemDataField("Raise Delay", ref _target.data.RaiseDelay, dirtyMesh: false);
				ItemDataField("Depth Bias", ref _target.data.DepthBias, dirtyMesh: false);
				ItemDataField("Visible", ref _target.data.IsVisible);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position & Translation")) {
				EditorGUILayout.LabelField("Position");
				EditorGUI.indentLevel++;
				ItemDataField("", ref _target.data.Position);
				EditorGUI.indentLevel--;

				EditorGUILayout.LabelField("Scale");
				EditorGUI.indentLevel++;
				ItemDataField("", ref _target.data.Size);
				EditorGUI.indentLevel--;

				ItemDataField("Orientation", ref _target.data.RotZ);

			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				ItemDataField("Has Hit Event", ref _target.data.UseHitEvent, dirtyMesh: false);
				ItemDataField("Hit Threshold", ref _target.data.Threshold, dirtyMesh: false);

				EditorGUI.BeginDisabledGroup(_target.data.OverwritePhysics);
				MaterialField("Physics Material", ref _target.data.PhysicsMaterial, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Overwrite Material Settings", ref _target.data.OverwritePhysics, dirtyMesh: false);

				EditorGUI.BeginDisabledGroup(!_target.data.OverwritePhysics);
				ItemDataField("Elasticity", ref _target.data.Elasticity, dirtyMesh: false);
				ItemDataField("Elasticity Falloff", ref _target.data.ElasticityFalloff, dirtyMesh: false);
				ItemDataField("Friction", ref _target.data.Friction, dirtyMesh: false);
				ItemDataField("Scatter Angle", ref _target.data.Scatter, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Legacy Mode", ref _target.data.IsLegacy, dirtyMesh: false);
				ItemDataField("Collidable", ref _target.data.IsCollidable, dirtyMesh: false);
				ItemDataField("Is Dropped", ref _target.data.IsDropped, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _target.data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _target.data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
