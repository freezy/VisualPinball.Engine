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
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SpinnerColliderComponent)), CanEditMultipleObjects]
	public class SpinnerColliderInspector : ColliderInspector<SpinnerData, SpinnerComponent, SpinnerColliderComponent>
	{
		private SerializedProperty _isKinematicProperty;
		private SerializedProperty _elasticityProperty;

		protected override void OnEnable()
		{
			base.OnEnable();
			_isKinematicProperty = serializedObject.FindProperty(nameof(SpinnerColliderComponent._isKinematic));
			_elasticityProperty = serializedObject.FindProperty(nameof(SpinnerColliderComponent.Elasticity));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_isKinematicProperty, "Movable");
			PropertyField(_elasticityProperty, updateTransforms: true);

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
