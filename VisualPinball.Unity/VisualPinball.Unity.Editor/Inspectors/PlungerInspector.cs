using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Editor.Utils;
using VisualPinball.Unity.VPT.Plunger;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(PlungerBehavior))]
	public class PlungerInspector : ItemInspector
	{
		private PlungerBehavior _plunger;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutStateAndPhysics = true;
		private bool _foldoutMisc = true;

		private static string[] _plungerTypeStrings = { "Modern", "Flat", "Custom" };
		private static int[] _plungerTypeValues = { PlungerType.PlungerTypeModern, PlungerType.PlungerTypeFlat, PlungerType.PlungerTypeCustom };

		protected override void OnEnable()
		{
			base.OnEnable();
			_plunger = target as PlungerBehavior;
		}

		public class PlungerType2
		{
			public const int PlungerTypeModern = 1;
			public const int PlungerTypeFlat = 2;
			public const int PlungerTypeCustom = 3;
		}

		public override void OnInspectorGUI()
		{
			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DataFieldUtils.DropDownField("Type", ref _plunger.data.Type, _plungerTypeStrings, _plungerTypeValues, FinishEdit);
				MaterialField("Material", ref _plunger.data.Material);
				// TODO: Image Fields
				DataFieldUtils.ItemDataField("Flat Frames", ref _plunger.data.AnimFrames, FinishEdit);
				DataFieldUtils.ItemDataField("Width", ref _plunger.data.Width, FinishEdit);
				DataFieldUtils.ItemDataField("Z Adjustment", ref _plunger.data.ZAdjust, FinishEdit);
				DataFieldUtils.ItemDataField("Reflection Enabled", ref _plunger.data.IsReflectionEnabled, FinishEdit);
				EditorGUILayout.LabelField("Custom Settings");
				EditorGUI.indentLevel++;
				DataFieldUtils.ItemDataField("Rod Diameter", ref _plunger.data.RodDiam, FinishEdit);
				DataFieldUtils.ItemDataField("Tip Shape", ref _plunger.data.TipShape, FinishEdit); // TODO: break this down and provide individual fields
				DataFieldUtils.ItemDataField("Ring Gap", ref _plunger.data.RingGap, FinishEdit);
				DataFieldUtils.ItemDataField("Ring Diam", ref _plunger.data.RingDiam, FinishEdit);
				DataFieldUtils.ItemDataField("Ring Width", ref _plunger.data.RingWidth, FinishEdit);
				DataFieldUtils.ItemDataField("Spring Diam", ref _plunger.data.SpringDiam, FinishEdit);
				DataFieldUtils.ItemDataField("Spring Gauge", ref _plunger.data.SpringGauge, FinishEdit);
				DataFieldUtils.ItemDataField("Spring Loops", ref _plunger.data.SpringLoops, FinishEdit);
				DataFieldUtils.ItemDataField("End Loops", ref _plunger.data.SpringEndLoops, FinishEdit);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				DataFieldUtils.ItemDataField("", ref _plunger.data.Center, FinishEdit);
				SurfaceField("Surface", ref _plunger.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutStateAndPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutStateAndPhysics, "State & Physics")) {
				DataFieldUtils.ItemDataField("Pull Speed", ref _plunger.data.SpeedPull, FinishEdit, ("dirtyMesh",false));
				DataFieldUtils.ItemDataField("Release Speed", ref _plunger.data.SpeedFire, FinishEdit, ("dirtyMesh",false));
				DataFieldUtils.ItemDataField("Stroke Length", ref _plunger.data.Stroke, FinishEdit, ("dirtyMesh",false));
				DataFieldUtils.ItemDataField("Scatter Velocity", ref _plunger.data.ScatterVelocity, FinishEdit, ("dirtyMesh",false));
				DataFieldUtils.ItemDataField("Enable Mechanical Plunger", ref _plunger.data.IsMechPlunger, FinishEdit, ("dirtyMesh",false));
				DataFieldUtils.ItemDataField("Auto Plunger", ref _plunger.data.AutoPlunger, FinishEdit, ("dirtyMesh",false));
				DataFieldUtils.ItemDataField("Visible", ref _plunger.data.IsVisible, FinishEdit);
				DataFieldUtils.ItemDataField("Mech Strength", ref _plunger.data.MechStrength, FinishEdit, ("dirtyMesh",false));
				DataFieldUtils.ItemDataField("Momentum Xfer", ref _plunger.data.MomentumXfer, FinishEdit, ("dirtyMesh",false));
				DataFieldUtils.ItemDataField("Park Position (0..1)", ref _plunger.data.ParkPosition, FinishEdit, ("dirtyMesh",false));
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				DataFieldUtils.ItemDataField("Timer Enabled", ref _plunger.data.IsTimerEnabled, FinishEdit, ("dirtyMesh",false));
				DataFieldUtils.ItemDataField("Timer Interval", ref _plunger.data.TimerInterval, FinishEdit, ("dirtyMesh",false));
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
