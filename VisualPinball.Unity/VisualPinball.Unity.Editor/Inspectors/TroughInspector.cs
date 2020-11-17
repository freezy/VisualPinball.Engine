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
using VisualPinball.Engine.VPT.Trough;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(TroughAuthoring))]
	public class TroughInspector : ItemMainInspector<Trough, TroughData, TroughAuthoring>
	{
		static GUIContent[] s_popupOptions = {
			new GUIContent("Entry Kicker"),
			new GUIContent("Entry Trigger"),
		};

		private int m_entryOption = 0;

		protected override void OnEnable()
		{
			if (string.IsNullOrEmpty(Data.EntryKicker)) {
				m_entryOption = 1;
			}
			else {
				m_entryOption = 0;
			}

			base.OnEnable();
		}

		public override void OnInspectorGUI()
		{
			EditorGUILayout.BeginHorizontal();
			m_entryOption = EditorGUILayout.Popup(m_entryOption, s_popupOptions);
			if (m_entryOption > 0) {
				ItemReferenceField<TriggerAuthoring, Trigger, TriggerData>("", "entryTrigger", ref Data.EntryTrigger);
			}
			else {
				ItemReferenceField<KickerAuthoring, Kicker, KickerData>("", "entryKicker", ref Data.EntryKicker);
			}
			EditorGUILayout.EndHorizontal();

			ItemReferenceField<KickerAuthoring, Kicker, KickerData>("Exit Kicker", "exitKicker", ref Data.ExitKicker);
			ItemReferenceField<TriggerAuthoring, Trigger, TriggerData>("Jam Switch", "jamSwitch", ref Data.JamSwitch);

			ItemDataField("Max Balls", ref Data.BallCount, false);
			ItemDataField("Switch Count", ref Data.SwitchCount, false);
			ItemDataField("Settle Time", ref Data.SettleTime, false);
		}

		protected override void FinishEdit(string label, bool dirtyMesh = true)
		{
			base.FinishEdit(label, dirtyMesh);
			ItemAuthoring.UpdatePosition();
		}
	}
}
