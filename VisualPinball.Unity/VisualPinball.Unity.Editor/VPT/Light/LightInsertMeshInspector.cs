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
using VisualPinball.Engine.VPT.Light;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(LightInsertMeshComponent)), CanEditMultipleObjects]
	public class LightInsertMeshInspector : MeshInspector<LightData, LightComponent, LightInsertMeshComponent>
	{
		private SerializedProperty _insertHeightProperty;
		private SerializedProperty _positionZProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_insertHeightProperty = serializedObject.FindProperty(nameof(LightInsertMeshComponent.InsertHeight));
			_positionZProperty = serializedObject.FindProperty(nameof(LightInsertMeshComponent.PositionZ));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_insertHeightProperty, rebuildMesh: true);
			PropertyField(_positionZProperty, updateTransforms: true);

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
