// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

using UnityEditor;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PlayfieldComponent)), CanEditMultipleObjects]
	public class PlayfieldInspector : MainInspector<TableData, PlayfieldComponent>
	{
		private SerializedProperty _rightProperty;
		private SerializedProperty _bottomProperty;
		private SerializedProperty _glassHeightProperty;
		private SerializedProperty _angleTiltMinProperty;
		private SerializedProperty _angleTiltMaxProperty;
		private SerializedProperty _renderSlopeProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_rightProperty = serializedObject.FindProperty(nameof(PlayfieldComponent.Right));
			_bottomProperty = serializedObject.FindProperty(nameof(PlayfieldComponent.Bottom));
			_glassHeightProperty = serializedObject.FindProperty(nameof(PlayfieldComponent.GlassHeight));
			_angleTiltMinProperty = serializedObject.FindProperty(nameof(PlayfieldComponent.AngleTiltMin));
			_angleTiltMaxProperty = serializedObject.FindProperty(nameof(PlayfieldComponent.AngleTiltMax));
			_renderSlopeProperty = serializedObject.FindProperty(nameof(PlayfieldComponent.RenderSlope));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_rightProperty, "Table Width", true);
			PropertyField(_bottomProperty, "Table Height/Length", true);
			PropertyField(_glassHeightProperty, "Top Glass Height", true);
			PropertyField(_angleTiltMinProperty, "Slope for Min. Difficulty");
			PropertyField(_angleTiltMaxProperty, "Slope for Max. Difficulty");
			PropertyField(_renderSlopeProperty, "Rendered Playfield Angle");

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
