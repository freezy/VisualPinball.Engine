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
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Flipper;
using Color = UnityEngine.Color;
using Mesh = UnityEngine.Mesh;

namespace VisualPinball.Unity
{
	public abstract class ItemColliderAuthoring<TItem, TData, TMainAuthoring> : ItemSubAuthoring<TItem, TData, TMainAuthoring>,
		IItemColliderAuthoring
		where TData : ItemData
		where TItem : Item<TData>, IHittable, IRenderable
		where TMainAuthoring : ItemMainRenderableAuthoring<TItem, TData>
	{
		[NonSerialized]
		public bool ShowGizmos;

		[SerializeField]
		public bool ShowColliderMesh;

		[SerializeField]
		public bool ShowAabbs;

		[NonSerialized]
		public int SelectedCollider = -1;

		public List<ICollider> Colliders { get; private set; }

		public new IItemMainRenderableAuthoring MainAuthoring => base.MainAuthoring;

		private readonly Entity _colliderEntity = new Entity {Index = -2, Version = 0};

		protected abstract IColliderGenerator InstantiateColliderApi(Player player, Entity entity, Entity parentEntity);

		private void OnDrawGizmosSelected()
		{
			if (!ShowGizmos || !ShowAabbs && !ShowColliderMesh) {
				return;
			}

			var player = GetComponentInParent<Player>();
			if (player == null) {
				return;
			}
			var api = InstantiateColliderApi(player, _colliderEntity, Entity.Null);
			Colliders = new List<ICollider>();
			api.CreateColliders(Table, Colliders);


			var ltw = transform.GetComponentInParent<TableAuthoring>().gameObject.transform.localToWorldMatrix;

			// draw aabbs and colliders
			for (var i = 0; i < Colliders.Count; i++) {
				var col = Colliders[i];
				if (ShowAabbs) {
					DrawAabb(ltw, col.Bounds.Aabb, i == SelectedCollider);
				}
				if (ShowColliderMesh) {
					DrawCollider(ltw, col, i == SelectedCollider);
				}
			}
		}

		#region Collider Gizmos

		private static void DrawAabb(Matrix4x4 ltw, Aabb aabb, bool isSelected)
		{
			var p00 = ltw.MultiplyPoint(new Vector3( aabb.Left, aabb.Top, aabb.ZHigh));
			var p01 = ltw.MultiplyPoint(new Vector3(aabb.Left, aabb.Bottom, aabb.ZHigh));
			var p02 = ltw.MultiplyPoint(new Vector3(aabb.Right, aabb.Bottom, aabb.ZHigh));
			var p03 = ltw.MultiplyPoint(new Vector3(aabb.Right, aabb.Top, aabb.ZHigh));

			var p10 = ltw.MultiplyPoint(new Vector3( aabb.Left, aabb.Top, aabb.ZLow));
			var p11 = ltw.MultiplyPoint(new Vector3(aabb.Left, aabb.Bottom, aabb.ZLow));
			var p12 = ltw.MultiplyPoint(new Vector3(aabb.Right, aabb.Bottom, aabb.ZLow));
			var p13 = ltw.MultiplyPoint(new Vector3(aabb.Right, aabb.Top, aabb.ZLow));

			Gizmos.color = isSelected ? ColliderColor.SelectedAabb : ColliderColor.Aabb;
			Gizmos.DrawLine(p00, p01);
			Gizmos.DrawLine(p01, p02);
			Gizmos.DrawLine(p02, p03);
			Gizmos.DrawLine(p03, p00);

			Gizmos.DrawLine(p10, p11);
			Gizmos.DrawLine(p11, p12);
			Gizmos.DrawLine(p12, p13);
			Gizmos.DrawLine(p13, p10);

			Gizmos.DrawLine(p00, p10);
			Gizmos.DrawLine(p01, p11);
			Gizmos.DrawLine(p02, p12);
			Gizmos.DrawLine(p03, p13);
		}

		private void DrawCollider(Matrix4x4 ltw, ICollider hitObject, bool isSelected)
		{
			Gizmos.color = isSelected ? ColliderColor.SelectedCollider : ColliderColor.Collider;
			switch (hitObject) {

				case PointCollider pointCol: {
					Gizmos.DrawSphere(ltw.MultiplyPoint(pointCol.P), 0.001f);
					break;
				}

				case LineCollider lineCol: {
					const int num = 10;
					var aabb = lineCol.Bounds.Aabb;
					var d = (aabb.ZHigh - aabb.ZLow) / num;
					for (var i = 0; i < num; i++) {
						Gizmos.DrawLine(
							ltw.MultiplyPoint(lineCol.V1.ToFloat3(aabb.ZLow + i * d)),
							ltw.MultiplyPoint(lineCol.V2.ToFloat3(aabb.ZLow + i * d))
						);
					}
					break;
				}

				// todo
				// case Line3DCollider line3DCol: {
				// 	Gizmos.DrawLine(
				// 		ltw.MultiplyPoint(line3DCol.V1.ToUnityVector3()),
				// 		ltw.MultiplyPoint(line3DCol.V2.ToUnityVector3())
				// 	);
				// 	break;
				// }

				case LineZCollider lineZCol: {
					var aabb = lineZCol.Bounds.Aabb;
					Gizmos.DrawLine(
						ltw.MultiplyPoint(lineZCol.XY.ToFloat3(aabb.ZLow)),
						ltw.MultiplyPoint(lineZCol.XY.ToFloat3(aabb.ZHigh))
					);
					break;
				}

				case TriangleCollider triangleCol: {
					Gizmos.DrawLine(
						ltw.MultiplyPoint(triangleCol.Rgv0),
						ltw.MultiplyPoint(triangleCol.Rgv1)
					);
					Gizmos.DrawLine(
						ltw.MultiplyPoint(triangleCol.Rgv1),
						ltw.MultiplyPoint(triangleCol.Rgv2)
					);
					Gizmos.DrawLine(
						ltw.MultiplyPoint(triangleCol.Rgv2),
						ltw.MultiplyPoint(triangleCol.Rgv0)
					);
					break;
				}

				case CircleCollider circleCol: {
					const int num = 20;
					var aabb = circleCol.Bounds.Aabb;
					var d = (aabb.ZHigh - aabb.ZLow) / num;
					for (var i = 0; i < num; i++) {
						GizmoDrawCircle(ltw, circleCol.Center.ToFloat3(aabb.ZLow + i * d), circleCol.Radius);
					}
					break;
				}

				case GateCollider gateCol: {
					DrawCollider(ltw, gateCol.LineSeg0, isSelected);
					DrawCollider(ltw, gateCol.LineSeg1, isSelected);
					break;
				}

				case SpinnerCollider spinnerCol: {
					DrawCollider(ltw, spinnerCol.LineSeg0, isSelected);
					DrawCollider(ltw, spinnerCol.LineSeg1, isSelected);
					break;
				}

				case FlipperCollider _: {

					Mesh mesh = null;

					// first see if we already have a mesh
					var meshAuthoring = GetComponentInChildren<FlipperRubberMeshAuthoring>();
					if (meshAuthoring != null) {
						var meshComponent = meshAuthoring.gameObject.GetComponent<MeshFilter>();
						if (meshComponent != null) {
							mesh = meshComponent.sharedMesh;
						}
					}

					if (mesh == null) {
						var ro = Item.GetRenderObject(Table, FlipperMeshGenerator.Rubber, Origin.Original);
						mesh = ro.Mesh.ToUnityMesh();
					}

					var t = gameObject.transform;
					Gizmos.DrawWireMesh(mesh, t.position, t.rotation, t.lossyScale);
					break;
				}

				case PlungerCollider plungerCol: {
					DrawCollider(ltw, plungerCol.LineSegBase, isSelected);
					DrawCollider(ltw, plungerCol.JointBase0, isSelected);
					DrawCollider(ltw, plungerCol.JointBase1, isSelected);
					break;
				}
			}
		}

		private static void GizmoDrawCircle(Matrix4x4 ltw, Vector3 center, float radius)
		{
			var theta = 0f;
			var x = radius * MathF.Cos(theta);
			var y = radius * MathF.Sin(theta);
			var pos = center + new Vector3(x, y, 0f);
			var lastPos = pos;
			for (theta = 0.1f; theta < MathF.PI * 2; theta += 0.1f){
				x = radius * MathF.Cos(theta);
				y = radius * MathF.Sin(theta);
				var newPos = center + new Vector3(x, y, 0);
				Gizmos.DrawLine(
					ltw.MultiplyPoint(pos),
					ltw.MultiplyPoint(newPos)
				);
				pos = newPos;
			}
			Gizmos.DrawLine(pos, lastPos);
		}

		#endregion
	}

	internal static class ColliderColor
	{
		internal static readonly Color Aabb = new Color32(255, 0, 252, 50);
		internal static readonly Color SelectedAabb = new Color32(255, 0, 252, 255);
		internal static readonly Color Collider = new Color32(0, 255, 75, 50);
		internal static readonly Color SelectedCollider = new Color32(0, 255, 75, 255);
	}
}
