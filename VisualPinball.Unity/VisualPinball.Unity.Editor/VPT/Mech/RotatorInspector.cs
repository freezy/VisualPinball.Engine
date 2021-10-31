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
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RotatorComponent)), CanEditMultipleObjects]
	public class RotatorInspector : ItemInspector
	{
		private SerializedProperty _targetProperty;
		private SerializedProperty _rotateWithProperty;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();

			_targetProperty = serializedObject.FindProperty(nameof(RotatorComponent._target));
			_rotateWithProperty = serializedObject.FindProperty(nameof(RotatorComponent._rotateWith));
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_targetProperty);
			PropertyField(_rotateWithProperty);

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
