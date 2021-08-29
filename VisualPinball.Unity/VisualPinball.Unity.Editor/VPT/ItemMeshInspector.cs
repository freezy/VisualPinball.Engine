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
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	public class ItemMeshInspector<TData, TMainAuthoring, TMeshAuthoring> : ItemInspector
		where TMeshAuthoring : ItemMeshAuthoring<TData, TMainAuthoring>
		where TData : ItemData
		where TMainAuthoring : ItemMainRenderableAuthoring<TData>
	{
		protected TMeshAuthoring MeshAuthoring;

		protected TData Data => MeshAuthoring == null ? null : MeshAuthoring.Data;

		protected override MonoBehaviour UndoTarget => MeshAuthoring.MainComponent;

		protected override void OnEnable()
		{
			MeshAuthoring = target as TMeshAuthoring;
			base.OnEnable();
		}

		public override void OnInspectorGUI()
		{
			if (MeshAuthoring == null) {
				return;
			}

			if (GUILayout.Button("Force Update Mesh")) {
				MeshAuthoring.RebuildMeshes();
			}
		}

		protected bool HasErrors()
		{
			if (Data == null) {
				NoDataError();
				return true;
			}

			if (!MeshAuthoring.IsCorrectlyParented) {
				InvalidParentError();
				return true;
			}

			return false;
		}

		private static void NoDataError()
		{
			EditorGUILayout.HelpBox($"Cannot find main component!\n\nYou must have a {typeof(TMainAuthoring).Name} component on either this GameObject, its parent or grand parent.", MessageType.Error);
		}

		private void InvalidParentError()
		{
			var validParentTypes = MeshAuthoring.ValidParents.ToArray();
			var typeMessage = validParentTypes.Length > 0
				? $"Supported parents are: [ {string.Join(", ", validParentTypes.Select(t => t.Name))} ]."
				: $"In this case, meshes for {MeshAuthoring.ItemName} don't support any parenting at all.";
			EditorGUILayout.HelpBox($"Invalid parent. This {MeshAuthoring.ItemName} is parented to a {MeshAuthoring.ParentAuthoring.ItemName}, which VPE doesn't support.\n{typeMessage}", MessageType.Error);
			if (GUILayout.Button("Open Documentation", EditorStyles.linkLabel)) {
				Application.OpenURL("https://docs.visualpinball.org/creators-guide/editor/unity-components.html");
			}
		}
	}
}
