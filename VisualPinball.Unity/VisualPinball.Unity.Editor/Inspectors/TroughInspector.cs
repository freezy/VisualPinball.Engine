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

		public override void OnInspectorGUI()
		{
			DropDownField("Type", ref Data.Type, TypeLabels, TypeValues);

			if (Data.Type != TroughType.ClassicSingleBall) {
				ItemDataSlider("Ball Count", ref Data.BallCount, 1, 10, false);
			}

			switch (Data.Type) {
				case TroughType.ModernOpto:
				case TroughType.ModernMech:
				case TroughType.TwoCoilsNSwitches:
					ItemDataSlider("Switch Count", ref Data.SwitchCount, 1, 10, false);
					ItemDataField("Has Jam Switch", ref Data.JamSwitch, false);
					break;
				case TroughType.TwoCoilsOneSwitch:
					ItemDataSlider("Switch Position", ref Data.SwitchCount, 1, 10, false);
					ItemDataField("Has Jam Switch", ref Data.JamSwitch, false);
					break;
			}

			if (Data.JamSwitch || Data.Type != TroughType.ModernOpto && Data.Type != TroughType.ModernMech && Data.Type != TroughType.TwoCoilsNSwitches) {
				ItemDataField("Kick Time (ms)", ref Data.KickTime, false);
			}

			ItemDataField("Roll Time (ms)", ref Data.RollTime, false);
			if (Data.Type == TroughType.ModernOpto) {
				ItemDataField("Transition Time (ms)", ref Data.TransitionTime, false);
			}

			if (!Application.isPlaying) {
				if (_togglePlayfield = EditorGUILayout.BeginFoldoutHeaderGroup(_togglePlayfield, "Playfield Links")) {
					EditorGUI.indentLevel++;
					ObjectReferenceField<ISwitchAuthoring>("Input Switch", "Switches", "None (Switch)", "inputSwitch", Data.PlayfieldEntrySwitch, n => Data.PlayfieldEntrySwitch = n);
					ObjectReferenceField<KickerAuthoring>("Exit Kicker", "Kickers", "None (Kicker)", "exitKicker", Data.PlayfieldExitKicker, n => Data.PlayfieldExitKicker = n);
					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndFoldoutHeaderGroup();
			}

			if (Application.isPlaying) {
				EditorGUILayout.Separator();


				GUILayout.BeginHorizontal();
				GUILayout.BeginVertical();

				EditorGUILayout.LabelField("Switch status:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
				var troughApi = _table.GetComponent<Player>().TableApi.Trough(Item.Name);

				if (Data.Type != TroughType.ModernOpto && Data.Type != TroughType.ModernMech) {
					DrawSwitch("Drain Switch", troughApi.EntrySwitch);
				}

				if (Data.Type == TroughType.TwoCoilsOneSwitch) {
					DrawSwitch("Stack Switch", troughApi.StackSwitch());

				} else if (Data.Type != TroughType.ClassicSingleBall) {
					for (var i = troughApi.NumStackSwitches - 1; i >= 0; i--) {
						DrawSwitch(SwitchDescription(i), troughApi.StackSwitch(i));
					}
				}

				if (Data.JamSwitch) {
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

				if (Data.Type != TroughType.ModernOpto && Data.Type != TroughType.ModernMech && Data.Type != TroughType.ClassicSingleBall) {
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
