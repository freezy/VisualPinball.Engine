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

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity.Editor
{
	public abstract class TargetInspector : ItemMainInspector<HitTargetData, TargetComponent>
	{
		private SerializedProperty _positionProperty;
		private SerializedProperty _rotationProperty;
		private SerializedProperty _sizeProperty;
		private SerializedProperty _meshNameProperty;
		private SerializedProperty _typeNameProperty;

		protected abstract string MeshAssetFolder { get; }
		protected abstract Dictionary<string, int> MeshTypeMapping { get; }

		protected override void OnEnable()
		{
			base.OnEnable();

			_positionProperty = serializedObject.FindProperty(nameof(TargetComponent.Position));
			_rotationProperty = serializedObject.FindProperty(nameof(TargetComponent.Rotation));
			_sizeProperty = serializedObject.FindProperty(nameof(TargetComponent.Size));
			_meshNameProperty = serializedObject.FindProperty(nameof(TargetComponent._meshName));
			_typeNameProperty = serializedObject.FindProperty(nameof(TargetComponent._targetType));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_positionProperty, updateTransforms: true);
			PropertyField(_rotationProperty, updateTransforms: true);
			PropertyField(_sizeProperty, updateTransforms: true);

			MeshDropdownProperty("Mesh", _meshNameProperty, MeshAssetFolder, MainComponent.gameObject, _typeNameProperty, MeshTypeMapping);

			EndEditing();
		}
	}
}
