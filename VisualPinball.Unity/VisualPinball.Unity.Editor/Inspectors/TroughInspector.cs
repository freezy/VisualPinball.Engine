// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
			"Modern (opto or mechanical)",
			"Two coils multiple switches",
			"Two coils one switch",
			"Classic single ball",
		};

		private static readonly int[] TypeValues = {
			TroughType.Modern,
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
				case TroughType.Modern:
				case TroughType.TwoCoilsNSwitches:
					ItemDataSlider("Switch Count", ref Data.SwitchCount, 1, 10, false);
					break;
				case TroughType.TwoCoilsOneSwitch:
					ItemDataSlider("Switch Position", ref Data.SwitchCount, 1, 10, false);
					break;
			}
			ItemDataField("Kick Time (ms)", ref Data.RollTime, false);
			ItemDataField("Roll Time (ms)", ref Data.KickTime, false);

			if (!Application.isPlaying) {
				if (_togglePlayfield = EditorGUILayout.BeginFoldoutHeaderGroup(_togglePlayfield, "Playfield Hooks")) {
					EditorGUI.indentLevel++;
					ObjectReferenceField<ISwitchAuthoring>("Input Switch", "Switches", "None (Switch)", "inputSwitch", Data.PlayfieldEntrySwitch, n => Data.PlayfieldEntrySwitch = n);
					ObjectReferenceField<KickerAuthoring>("Exit Kicker", "Kickers", "None (Kicker)", "exitKicker", Data.PlayfieldExitKicker, n => Data.PlayfieldExitKicker = n);
					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndFoldoutHeaderGroup();
			}

			if (Application.isPlaying) {
				EditorGUILayout.Separator();
				EditorGUILayout.LabelField("Switch status:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

				var troughApi = _table.GetComponent<Player>().TableApi.Trough(Item.Name);

				EditorGUI.BeginDisabledGroup(true);
				if (Data.Type != TroughType.Modern) {
					EditorGUILayout.Toggle("Drain Switch", troughApi.EntrySwitch.IsClosed);
				}

				if (Data.Type == TroughType.TwoCoilsOneSwitch) {
					EditorGUILayout.Toggle("Stack Switch", troughApi.StackSwitch().IsClosed);

				} else if (Data.Type != TroughType.ClassicSingleBall) {
					for (var i = troughApi.NumBallSwitches - 1; i >= 0; i--) {
						EditorGUILayout.Toggle(SwitchDescription(i), troughApi.StackSwitch(i).IsClosed);
					}
				}

				if (troughApi.UncountedDrainBalls > 0) {
					EditorGUILayout.LabelField("Undrained balls:", troughApi.UncountedDrainBalls.ToString());
				}
				if (troughApi.UncountedStackBalls > 0) {
					EditorGUILayout.LabelField("Unswitched balls:", troughApi.UncountedStackBalls.ToString());
				}
				EditorGUI.EndDisabledGroup();
			}
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
