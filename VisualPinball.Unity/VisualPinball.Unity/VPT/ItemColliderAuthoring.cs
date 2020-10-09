// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Spinner;
using Color = UnityEngine.Color;

namespace VisualPinball.Unity
{
	public abstract class ItemColliderAuthoring<TItem, TData, TMainAuthoring> : ItemSubAuthoring<TItem, TData, TMainAuthoring>,
		IItemColliderAuthoring
		where TData : ItemData
		where TItem : Item<TData>, IHittable, IRenderable
		where TMainAuthoring : ItemMainAuthoring<TItem, TData>
	{
		[NonSerialized]
		public bool ShowGizmos;

		[SerializeField]
		public bool ShowColliderMesh;

		[SerializeField]
		public bool ShowAabbs;

		[NonSerialized]
		public int SelectedCollider = -1;

		public HitObject[] HitObjects { get; private set; }

		private void OnDrawGizmosSelected()
		{
			if (!ShowGizmos || !ShowAabbs && !ShowColliderMesh) {
				return;
			}

			var item = Item;
			if (item == null) {
				return;
			}

			var ltw = transform.GetComponentInParent<TableAuthoring>().gameObject.transform.localToWorldMatrix;
			item.Init(Table);
			HitObjects = item.GetHitShapes();

			// draw aabbs and colliders
			for (var i = 0; i < HitObjects.Length; i++) {
				var hit = HitObjects[i];
				if (ShowAabbs) {
					hit.CalcHitBBox();
					DrawAabb(ltw, hit.HitBBox, i == SelectedCollider);
				}
				if (ShowColliderMesh) {
					DrawCollider(ltw, hit, i == SelectedCollider);
				}
			}
		}

		#region Collider Gizmos

		private static void DrawAabb(Matrix4x4 ltw, Rect3D aabb, bool isSelected)
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

		private void DrawCollider(Matrix4x4 ltw, HitObject hitObject, bool isSelected)
		{
			Gizmos.color = isSelected ? ColliderColor.SelectedCollider : ColliderColor.Collider;
			switch (hitObject) {

				case HitPoint hitPoint: {
					Gizmos.DrawSphere(ltw.MultiplyPoint(hitPoint.P.ToUnityVector3()), 0.001f);
					break;
				}

				case LineSeg lineSeg: {
					const int num = 10;
					var d = (lineSeg.HitBBox.ZHigh - lineSeg.HitBBox.ZLow) / num;
					for (var i = 0; i < num; i++) {
						Gizmos.DrawLine(
							ltw.MultiplyPoint(lineSeg.V1.ToUnityVector3(lineSeg.HitBBox.ZLow + i * d)),
							ltw.MultiplyPoint(lineSeg.V2.ToUnityVector3(lineSeg.HitBBox.ZLow + i * d))
						);
					}
					break;
				}

				case HitLine3D hitLine3D: {
					Gizmos.DrawLine(
						ltw.MultiplyPoint(hitLine3D.V1.ToUnityVector3()),
						ltw.MultiplyPoint(hitLine3D.V2.ToUnityVector3())
					);
					break;
				}

				case HitLineZ hitLineZ: {
					Gizmos.DrawLine(
						ltw.MultiplyPoint(hitLineZ.Xy.ToUnityVector3(hitLineZ.HitBBox.ZLow)),
						ltw.MultiplyPoint(hitLineZ.Xy.ToUnityVector3(hitLineZ.HitBBox.ZHigh))
					);
					break;
				}

				case HitTriangle hitTriangle: {
					Gizmos.DrawLine(
						ltw.MultiplyPoint(hitTriangle.Rgv[0].ToUnityVector3()),
						ltw.MultiplyPoint(hitTriangle.Rgv[1].ToUnityVector3())
					);
					Gizmos.DrawLine(
						ltw.MultiplyPoint(hitTriangle.Rgv[1].ToUnityVector3()),
						ltw.MultiplyPoint(hitTriangle.Rgv[2].ToUnityVector3())
					);
					Gizmos.DrawLine(
						ltw.MultiplyPoint(hitTriangle.Rgv[2].ToUnityVector3()),
						ltw.MultiplyPoint(hitTriangle.Rgv[0].ToUnityVector3())
					);
					break;
				}

				case HitCircle hitCircle: {
					const int num = 20;
					var d = (hitCircle.HitBBox.ZHigh - hitCircle.HitBBox.ZLow) / num;
					for (var i = 0; i < num; i++) {
						GizmoDrawCircle(ltw, hitCircle.Center.ToUnityVector3(hitCircle.HitBBox.ZLow + i * d), hitCircle.Radius);
					}
					break;
				}

				case GateHit gateHit: {
					DrawCollider(ltw, gateHit.LineSeg0, isSelected);
					DrawCollider(ltw, gateHit.LineSeg1, isSelected);
					break;
				}

				case SpinnerHit spinnerHit: {
					DrawCollider(ltw, spinnerHit.LineSeg0, isSelected);
					DrawCollider(ltw, spinnerHit.LineSeg1, isSelected);
					break;
				}

				case FlipperHit flipperHit: {
					var mfs = GetComponentsInChildren<MeshFilter>();
					foreach (var mf in mfs) {
						if (mf.name == "Rubber") {
							var t = mf.transform;
							Gizmos.DrawWireMesh(mf.sharedMesh, t.position, t.rotation, t.lossyScale);
							break;
						}
					}
					break;
				}

				case PlungerHit plungerHit: {
					DrawCollider(ltw, plungerHit.LineSegBase, isSelected);
					DrawCollider(ltw, plungerHit.LineSegEnd, isSelected);
					DrawCollider(ltw, plungerHit.LineSegSide[0], isSelected);
					DrawCollider(ltw, plungerHit.LineSegSide[1], isSelected);
					DrawCollider(ltw, plungerHit.JointBase[0], isSelected);
					DrawCollider(ltw, plungerHit.JointBase[1], isSelected);
					DrawCollider(ltw, plungerHit.JointEnd[0], isSelected);
					DrawCollider(ltw, plungerHit.JointEnd[1], isSelected);
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
