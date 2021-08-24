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
	public class ItemMainInspector<TItem, TData, TMainAuthoring> : ItemInspector
		where TData : ItemData
		where TItem : Item<TData>
		where TMainAuthoring : ItemMainAuthoring<TItem, TData>
	{
		protected TMainAuthoring ItemAuthoring;
		protected TItem Item;
		protected TData Data;

		public override MonoBehaviour UndoTarget => ItemAuthoring;

		protected override void OnEnable()
		{
			ItemAuthoring = (TMainAuthoring)target;
			Item = ItemAuthoring.Item;
			Data = ItemAuthoring.Data;
			base.OnEnable();
		}

		protected bool HasErrors()
		{
			if (!ItemAuthoring.IsCorrectlyParented) {
				InvalidParentError();
				return true;
			}

			return false;
		}

		protected void InvalidParentError()
		{
			var validParentTypes = ItemAuthoring.ValidParents.ToArray();
			var typeMessage = validParentTypes.Length > 0
				? $"Supported parents are: [ {string.Join(", ", validParentTypes.Select(t => t.Name))} ]."
				: $"In this case, {Item.ItemName} doesn't support any parenting at all.";
			EditorGUILayout.HelpBox($"Invalid parent. This {Item.ItemName} is parented to a {ItemAuthoring.ParentAuthoring.IItem.ItemName}, which VPE doesn't support.\n{typeMessage}", MessageType.Error);
			if (GUILayout.Button("Open Documentation", EditorStyles.linkLabel)) {
				Application.OpenURL("https://docs.visualpinball.org/creators-guide/editor/unity-components.html");
			}
		}

		protected void UpdateSurfaceReferences(Transform obj)
		{
			var surfaceAuthoring = obj.gameObject.GetComponent<IOnSurfaceAuthoring>();
			if (surfaceAuthoring != null && surfaceAuthoring.Surface == ItemAuthoring) {
				surfaceAuthoring.OnSurfaceUpdated();
			}
		}

		protected void UpdateTableHeightReferences(Transform obj)
		{
			var onTableAuthoring = obj.gameObject.GetComponent<IOnPlayfieldAuthoring>();
			if (onTableAuthoring != null) {
				onTableAuthoring.OnPlayfieldHeightUpdated();
			}
		}
	}
}
