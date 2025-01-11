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
		public override void OnInspectorGUI()
		{
			// position
			EditorGUI.BeginChangeCheck();
			var newPos = EditorGUILayout.Vector3Field(new GUIContent("Position", "Position of the primitive on the playfield, relative to its parent."), MainComponent.Position);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Primitive Position");
				MainComponent.Position = newPos;
			}
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

				var mc = go.AddComponent<PrimitiveMeshComponent>();
				mc.UseLegacyMesh = false;

				var cc = go.AddComponent<PrimitiveColliderComponent>();
				cc.enabled = true;
			}
		}

		[MenuItem("GameObject/Visual Pinball/Make Collider", true, 21)]
		public static bool MakeColliderValidation()
		{
			return Selection.gameObjects.All(gameObject => gameObject.GetComponent<IMainComponent>() == null && gameObject.GetComponent<MeshFilter>() != null);
		}

		[MenuItem("GameObject/Visual Pinball/Make Collider", false, 21)]
		public static void MakeCollider()
		{
			foreach (var go in Selection.gameObjects) {
				go.AddComponent<PrimitiveComponent>();

				var mc = go.AddComponent<PrimitiveMeshComponent>();
				mc.UseLegacyMesh = false;
				mc.enabled = false;

				var cc = go.AddComponent<PrimitiveColliderComponent>();
				cc.enabled = true;
			}
		}
	}
}
