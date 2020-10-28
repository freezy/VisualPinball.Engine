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

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(KickerColliderAuthoring))]
	public class KickerColliderInspector : ItemColliderInspector<Kicker, KickerData, KickerAuthoring, KickerColliderAuthoring>
	{
		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			ItemDataField("Enabled", ref Data.IsEnabled, false);
			ItemDataField("Fall Through", ref Data.FallThrough, false);
			ItemDataField("Legacy", ref Data.LegacyMode, false);
			ItemDataField("Scatter Angle", ref Data.Scatter, false);
			ItemDataField("Hit Accuracy", ref Data.HitAccuracy, false);
			ItemDataField("Hit Height", ref Data.HitHeight, false);

			ItemDataField("Default Angle", ref Data.Angle, false);
			ItemDataField("Default Speed", ref Data.Speed, false);

			base.OnInspectorGUI();
		}
	}
}
