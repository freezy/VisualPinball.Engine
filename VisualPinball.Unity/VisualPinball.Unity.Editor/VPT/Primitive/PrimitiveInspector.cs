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

using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PrimitiveComponent)), CanEditMultipleObjects]
	public class PrimitiveInspector : MainInspector<PrimitiveData, PrimitiveComponent>
	{
		private SerializedProperty _positionProperty;
		private SerializedProperty _rotationProperty;
		private SerializedProperty _sizeProperty;
		private SerializedProperty _translationProperty;
		private SerializedProperty _objectRotationProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_positionProperty = serializedObject.FindProperty(nameof(PrimitiveComponent.Position));
			_rotationProperty = serializedObject.FindProperty(nameof(PrimitiveComponent.Rotation));
			_sizeProperty = serializedObject.FindProperty(nameof(PrimitiveComponent.Size));
			_translationProperty = serializedObject.FindProperty(nameof(PrimitiveComponent.Translation));
			_objectRotationProperty = serializedObject.FindProperty(nameof(PrimitiveComponent.ObjectRotation));
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
			PropertyField(_translationProperty, updateTransforms: true);
			PropertyField(_objectRotationProperty, updateTransforms: true);

			base.OnInspectorGUI();

			EndEditing();
		}

		[MenuItem("GameObject/Visual Pinball/Make Primitive", true, 20)]
		public static bool CheckContextMenu()
		{
			return Selection.gameObjects.All(gameObject => gameObject.GetComponent<IMainComponent>() == null);
		}

		[MenuItem("GameObject/Visual Pinball/Make Primitive", false, 20)]
		public static void MakePrimitive()
		{
			foreach (var go in Selection.gameObjects) {
				var mf = go.GetComponent<MeshFilter>();
				if (!mf) {
					continue;
				}
				var pc = go.AddComponent<PrimitiveComponent>();
				pc.Position = go.transform.localPosition;
				pc.Rotation = go.transform.localEulerAngles;
				pc.Size = go.transform.localScale;

				var mc = go.AddComponent<PrimitiveMeshComponent>();
				mc.UseLegacyMesh = false;

				var cc = go.AddComponent<PrimitiveColliderComponent>();
				cc.enabled = true;
			}
		}
	}
}
