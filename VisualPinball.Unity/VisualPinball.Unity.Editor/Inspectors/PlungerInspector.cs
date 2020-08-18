// ReSharper disable AssignmentInConditionalExpression

using UnityEditor;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PlungerAuthoring))]
	public class PlungerInspector : ItemInspector
	{
		private PlungerAuthoring _plunger;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutStateAndPhysics = true;
		private bool _foldoutMisc = true;

		private static readonly string[] PlungerTypeStrings = { "Modern", "Flat", "Custom" };
		private static readonly int[] PlungerTypeValues = { PlungerType.PlungerTypeModern, PlungerType.PlungerTypeFlat, PlungerType.PlungerTypeCustom };

		protected override void OnEnable()
		{
			base.OnEnable();
			_plunger = target as PlungerAuthoring;
		}

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DropDownField("Type", ref _plunger.data.Type, PlungerTypeStrings, PlungerTypeValues);
				MaterialField("Material", ref _plunger.data.Material);
				TextureField("Image", ref _plunger.data.Image);
				ItemDataField("Flat Frames", ref _plunger.data.AnimFrames);
				ItemDataField("Width", ref _plunger.data.Width);
				ItemDataField("Z Adjustment", ref _plunger.data.ZAdjust);
				EditorGUILayout.LabelField("Custom Settings");
				EditorGUI.indentLevel++;
				ItemDataField("Rod Diameter", ref _plunger.data.RodDiam);
				ItemDataField("Tip Shape", ref _plunger.data.TipShape); // TODO: break this down and provide individual fields
				ItemDataField("Ring Gap", ref _plunger.data.RingGap);
				ItemDataField("Ring Diam", ref _plunger.data.RingDiam);
				ItemDataField("Ring Width", ref _plunger.data.RingWidth);
				ItemDataField("Spring Diam", ref _plunger.data.SpringDiam);
				ItemDataField("Spring Gauge", ref _plunger.data.SpringGauge);
				ItemDataField("Spring Loops", ref _plunger.data.SpringLoops);
				ItemDataField("End Loops", ref _plunger.data.SpringEndLoops);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref _plunger.data.Center);
				SurfaceField("Surface", ref _plunger.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutStateAndPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutStateAndPhysics, "State & Physics")) {
				ItemDataField("Pull Speed", ref _plunger.data.SpeedPull, dirtyMesh: false);
				ItemDataField("Release Speed", ref _plunger.data.SpeedFire, dirtyMesh: false);
				ItemDataField("Stroke Length", ref _plunger.data.Stroke, dirtyMesh: false);
				ItemDataField("Scatter Velocity", ref _plunger.data.ScatterVelocity, dirtyMesh: false);
				ItemDataField("Enable Mechanical Plunger", ref _plunger.data.IsMechPlunger, dirtyMesh: false);
				ItemDataField("Auto Plunger", ref _plunger.data.AutoPlunger, dirtyMesh: false);
				ItemDataField("Visible", ref _plunger.data.IsVisible);
				ItemDataField("Mech Strength", ref _plunger.data.MechStrength, dirtyMesh: false);
				ItemDataField("Momentum Xfer", ref _plunger.data.MomentumXfer, dirtyMesh: false);
				ItemDataField("Park Position (0..1)", ref _plunger.data.ParkPosition, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _plunger.data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _plunger.data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
