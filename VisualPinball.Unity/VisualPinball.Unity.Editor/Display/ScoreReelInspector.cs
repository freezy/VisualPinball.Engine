// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(ScoreReelComponent))]
	public class ScoreReelInspector : ItemInspector
	{
		private SerializedProperty _directionProperty;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();
			_directionProperty = serializedObject.FindProperty(nameof(ScoreReelComponent.Direction));
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();

			PropertyField(_directionProperty);
			EditorGUILayout.HelpBox("Speed and delay are configured in the score reel display component.", MessageType.Info);

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
