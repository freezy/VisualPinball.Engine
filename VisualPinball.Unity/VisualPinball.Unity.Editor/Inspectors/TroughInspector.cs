// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

// ReSharper disable AssignmentInConditionalExpression

using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Trough;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(TroughAuthoring))]
	public class TroughInspector : ItemMainInspector<Trough, TroughData, TroughAuthoring>
	{
		private static readonly string[] TypeLabels = {
			"Modern Opto",
			"Modern Mechanical",
			"Two coils multiple switches",
			"Two coils one switch",
			"Classic single ball",
		};

		private static readonly int[] TypeValues = {
			TroughType.ModernOpto,
			TroughType.ModernMech,
			TroughType.TwoCoilsNSwitches,
			TroughType.TwoCoilsOneSwitch,
			TroughType.ClassicSingleBall
		};

		private bool _togglePlayfield = true;
		private SerializedProperty _typeProperty;
		private SerializedProperty _playfieldEntrySwitchProperty;
		private SerializedProperty _playfieldExitKickerProperty;
		private SerializedProperty _ballCountProperty;
		private SerializedProperty _switchCountProperty;
		private SerializedProperty _jamSwitchProperty;
		private SerializedProperty _rollTimeProperty;
		private SerializedProperty _transitionTimeProperty;
		private SerializedProperty _kickTimeProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_typeProperty = serializedObject.FindProperty(nameof(TroughAuthoring.Type));
			_playfieldEntrySwitchProperty = serializedObject.FindProperty(nameof(TroughAuthoring._playfieldEntrySwitch));
			_playfieldExitKickerProperty = serializedObject.FindProperty(nameof(TroughAuthoring.PlayfieldExitKicker));
			_ballCountProperty = serializedObject.FindProperty(nameof(TroughAuthoring.BallCount));
			_switchCountProperty = serializedObject.FindProperty(nameof(TroughAuthoring.SwitchCount));
			_jamSwitchProperty = serializedObject.FindProperty(nameof(TroughAuthoring.JamSwitch));
			_rollTimeProperty = serializedObject.FindProperty(nameof(TroughAuthoring.RollTime));
			_transitionTimeProperty = serializedObject.FindProperty(nameof(TroughAuthoring.TransitionTime));
			_kickTimeProperty = serializedObject.FindProperty(nameof(TroughAuthoring.KickTime));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			DropDownProperty("Type", _typeProperty, TypeLabels, TypeValues);

			if (ItemAuthoring.Type != TroughType.ClassicSingleBall) {
				PropertyField(_ballCountProperty);
			}

			switch (ItemAuthoring.Type) {
				case TroughType.ModernOpto:
				case TroughType.ModernMech:
				case TroughType.TwoCoilsNSwitches:
					PropertyField(_switchCountProperty);
					PropertyField(_jamSwitchProperty, "Has Jam Switch");
					break;
				case TroughType.TwoCoilsOneSwitch:
					PropertyField(_switchCountProperty, "Switch Position");
					PropertyField(_jamSwitchProperty, "Has Jam Switch");
					break;
			}

			if (ItemAuthoring.JamSwitch || ItemAuthoring.Type != TroughType.ModernOpto && ItemAuthoring.Type != TroughType.ModernMech && ItemAuthoring.Type != TroughType.TwoCoilsNSwitches) {
				PropertyField(_kickTimeProperty, "Kick Time (ms)");
			}

			PropertyField(_rollTimeProperty, "Roll Time (ms)");
			if (ItemAuthoring.Type == TroughType.ModernOpto) {
				PropertyField(_transitionTimeProperty, "Transition Time (ms)");
			}

			if (!Application.isPlaying) {
				if (_togglePlayfield = EditorGUILayout.BeginFoldoutHeaderGroup(_togglePlayfield, "Playfield Links")) {
					EditorGUI.indentLevel++;
					PropertyField(_playfieldEntrySwitchProperty, "Input Switch");
					PropertyField(_playfieldExitKickerProperty, "Exit Kicker");
					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndFoldoutHeaderGroup();
			}

			serializedObject.ApplyModifiedProperties();

			if (Application.isPlaying) {
				EditorGUILayout.Separator();

				GUILayout.BeginHorizontal();
				GUILayout.BeginVertical();

				EditorGUILayout.LabelField("Switch status:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
				var troughApi = _ta.GetComponent<Player>().TableApi.Trough(Item.Name);

				if (ItemAuthoring.Type != TroughType.ModernOpto && ItemAuthoring.Type != TroughType.ModernMech) {
					DrawSwitch("Drain Switch", troughApi.EntrySwitch);
				}

				if (ItemAuthoring.Type == TroughType.TwoCoilsOneSwitch) {
					DrawSwitch("Stack Switch", troughApi.StackSwitch());

				} else if (ItemAuthoring.Type != TroughType.ClassicSingleBall) {
					for (var i = troughApi.NumStackSwitches - 1; i >= 0; i--) {
						DrawSwitch(SwitchDescription(i), troughApi.StackSwitch(i));
					}
				}

				if (ItemAuthoring.JamSwitch) {
					DrawSwitch("Jam Switch", troughApi.JamSwitch);
				}

				if (troughApi.UncountedDrainBalls > 0) {
					EditorGUILayout.LabelField("Undrained balls:", troughApi.UncountedDrainBalls.ToString());
				}
				if (troughApi.UncountedStackBalls > 0) {
					EditorGUILayout.LabelField("Unswitched balls:", troughApi.UncountedStackBalls.ToString());
				}

				GUILayout.EndVertical();
				GUILayout.BeginVertical();

				EditorGUILayout.LabelField("Coil status:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

				if (ItemAuthoring.Type != TroughType.ModernOpto && ItemAuthoring.Type != TroughType.ModernMech && ItemAuthoring.Type != TroughType.ClassicSingleBall) {
					DrawCoil("Entry Coil", troughApi.EntryCoil);
				}

				DrawCoil("Eject Coil", troughApi.ExitCoil);

				GUILayout.EndVertical();
				GUILayout.EndHorizontal();
			}
		}

		private static void DrawSwitch(string label, DeviceSwitch sw)
		{
			var labelPos = EditorGUILayout.GetControlRect();
			labelPos.height = 18;
			var switchPos = new Rect((float) (labelPos.x + (double) EditorGUIUtility.labelWidth + 2.0), labelPos.y, labelPos.height, labelPos.height);
			GUI.Label(labelPos, label);
			GUI.DrawTexture(switchPos, Icons.Switch(sw.IsSwitchClosed, IconSize.Small, sw.IsSwitchClosed ? IconColor.Orange : IconColor.Gray));
		}

		private static void DrawCoil(string label, DeviceCoil coil)
		{
			var labelPos = EditorGUILayout.GetControlRect();
			labelPos.height = 18;
			var switchPos = new Rect((float) (labelPos.x + (double) EditorGUIUtility.labelWidth - 20.0), labelPos.y, labelPos.height, labelPos.height);
			GUI.Label(labelPos, label);
			GUI.DrawTexture(switchPos, Icons.Bolt(IconSize.Small, coil.IsEnabled ? IconColor.Orange : IconColor.Gray));
		}

		protected override void FinishEdit(string label, bool dirtyMesh = true)
		{
			base.FinishEdit(label, dirtyMesh);
			ItemAuthoring.UpdatePosition();
		}

		private static string SwitchDescription(int i)
		{
			return i == 0 ? "Ball 1 (eject)" : $"Ball {i + 1}";
		}
	}
}
