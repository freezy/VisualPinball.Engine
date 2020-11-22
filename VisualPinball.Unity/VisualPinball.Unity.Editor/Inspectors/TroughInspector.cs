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
				ItemDataField("Ball Count", ref Data.BallCount, false);
			}

			if (Data.Type == TroughType.Modern || Data.Type == TroughType.TwoCoilsNSwitches) {
				ItemDataField("Switch Count", ref Data.SwitchCount, false);
			}
			ItemDataField("Settle Time (ms)", ref Data.SettleTime, false);

			if (_togglePlayfield = EditorGUILayout.BeginFoldoutHeaderGroup(_togglePlayfield, "Playfield Hooks")) {
				EditorGUI.indentLevel++;
				ObjectReferenceField<ISwitchAuthoring>("Input Switch", "Switches", "None (Switch)", "inputSwitch", Data.EntrySwitch, n => Data.EntrySwitch = n);
				ObjectReferenceField<KickerAuthoring>("Exit Kicker", "Kickers", "None (Kicker)", "exitKicker", Data.ExitKicker, n => Data.ExitKicker = n);
				ObjectReferenceField<TriggerAuthoring>("Jam Trigger", "Triggers", "None (Trigger)", "JamTrigger", Data.JamTrigger, n => Data.JamTrigger = n);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (Application.isPlaying) {
				EditorGUILayout.Separator();
				EditorGUILayout.LabelField("Switch status:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

				var troughApi = _table.GetComponent<Player>().TableApi.Trough(Item.Name);

				EditorGUI.BeginDisabledGroup(true);
				for (var i = troughApi.NumBallSwitches - 1; i >= 0; i--) {
					EditorGUILayout.Toggle(SwitchDescription(i), troughApi.BallSwitch(i).IsClosed);
				}
				EditorGUI.EndDisabledGroup();
			}
		}

		protected override void FinishEdit(string label, bool dirtyMesh = true)
		{
			base.FinishEdit(label, dirtyMesh);
			ItemAuthoring.UpdatePosition();
		}

		private string SwitchDescription(int i)
		{
			if (i == 0) {
				return "Ball 1 (eject)";
			}

			return i == Data.SwitchCount - 1
				? $"Ball {i + 1} (entry)"
				: $"Ball {i + 1}";
		}
	}
}
