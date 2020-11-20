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
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Engine.VPT.Trough;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(TroughAuthoring))]
	public class TroughInspector : ItemMainInspector<Trough, TroughData, TroughAuthoring>
	{
		public override void OnInspectorGUI()
		{
			ObjectReferenceField<ISwitchAuthoring>("Input Switch", "Switches", "None (Switch)", "inputSwitch", Data.EntryTrigger, n => Data.EntryTrigger = n);
			// ItemReferenceField<KickerAuthoring, Kicker, KickerData>("Exit Kicker", "exitKicker", ref Data.ExitKicker);
			// ItemReferenceField<TriggerAuthoring, Trigger, TriggerData>("Jam Switch", "jamSwitch", ref Data.JamSwitch);
			ObjectReferenceField<KickerAuthoring>("Exit Kicker", "Kickers", "None (Kicker)", "exitKicker", Data.ExitKicker, n => Data.ExitKicker = n);
			ObjectReferenceField<TriggerAuthoring>("Jam Switch", "Triggers", "None (Trigger)", "JamSwitch", Data.JamSwitch, n => Data.JamSwitch = n);

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
