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

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(TeleporterComponent)), CanEditMultipleObjects]
	public class TeleporterInspector : ItemInspector
	{
		private SerializedProperty _bidirectionalProperty;
		private SerializedProperty _kickAfterTeleportationProperty;
		private SerializedProperty _kickDelayProperty;
		private SerializedProperty _fromKickerProperty;
		private SerializedProperty _toKickerProperty;

		protected override MonoBehaviour UndoTarget => target as MonoBehaviour;

		protected override void OnEnable()
		{
			base.OnEnable();

			_bidirectionalProperty = serializedObject.FindProperty(nameof(TeleporterComponent.Bidirectional));
			_kickAfterTeleportationProperty = serializedObject.FindProperty(nameof(TeleporterComponent.KickAfterTeleportation));
			_kickDelayProperty = serializedObject.FindProperty(nameof(TeleporterComponent.KickDelay));
			_fromKickerProperty = serializedObject.FindProperty(nameof(TeleporterComponent.FromKicker));
			_toKickerProperty = serializedObject.FindProperty(nameof(TeleporterComponent.ToKicker));
		}

		public override void OnInspectorGUI()
		{
			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_bidirectionalProperty, "Bi-Directional");
			PropertyField(_kickAfterTeleportationProperty, "Kick After Teleport");
			PropertyField(_kickDelayProperty, "Wait Before Kick (s)");

			PropertyField(_fromKickerProperty);
			PropertyField(_toKickerProperty);

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
