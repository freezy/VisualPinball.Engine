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

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VisualPinball.Unity
{
	public abstract class ItemMainRenderableComponent : MonoBehaviour
	{
		public virtual bool CanBeTransformed => true;

		public bool IsLocked { get; set; }

		protected abstract IEnumerable<Type> MeshAuthoringTypes { get; }

		protected IEnumerable<ItemMeshComponent> MeshComponents => MeshAuthoringTypes
			.SelectMany(type => GetComponentsInChildren(type, true))
			.Select(c => (ItemMeshComponent)c);

		public void SetMeshDirty()
		{
			foreach (var meshComponent in MeshComponents) {
				meshComponent.MeshDirty = true;
			}
		}

		public void RebuildMeshIfDirty()
		{
			foreach (var meshComponent in MeshComponents) {
				if (meshComponent.MeshDirty) {
					meshComponent.RebuildMeshes();
				}
			}

			// // update transform based on item data, but not for "Table" since its the effective "root" and the user might want to move it on their own
			// var ta = GetComponentInParent<TableAuthoring>();
			// if (ta != this) {
			// 	transform.SetFromMatrix(Item.TransformationMatrix(Table, Origin.Original).ToUnityMatrix());
			// }
		}

		protected virtual void OnDrawGizmos()
		{
			// handle dirty whenever scene view draws just in case a field or dependant changed and our
			// custom inspector window isn't up to process it
			//RebuildMeshIfDirty();

			// Draw invisible gizmos over top of the sub meshes of this item so clicking in the scene view
			// selects the item itself first, which is most likely what the user would want
			var mfs = GetComponentsInChildren<MeshFilter>();
			Gizmos.color = Color.clear;
			Gizmos.matrix = Matrix4x4.identity;
			foreach (var mf in mfs) {
				var t = mf.transform;
				if (mf.sharedMesh.vertexCount > 0) {
					Gizmos.DrawMesh(mf.sharedMesh, t.position, t.rotation, t.lossyScale);
				}
			}
		}

		public virtual ItemDataTransformType EditorPositionType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorPosition() => Vector3.zero;
		public virtual void SetEditorPosition(Vector3 pos) { }

		public virtual ItemDataTransformType EditorRotationType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorRotation() => Vector3.zero;
		public virtual void SetEditorRotation(Vector3 rot) { }

		public virtual ItemDataTransformType EditorScaleType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorScale() => Vector3.zero;
		public virtual void SetEditorScale(Vector3 rot) { }

	}
}
