using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NLog;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Table;
using Color = UnityEngine.Color;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public abstract class ItemAuthoring<TItem, TData> : MonoBehaviour, IEditableItemAuthoring, IIdentifiableItemAuthoring,
		ILayerableItemAuthoring where TData : ItemData where TItem : Item<TData>, IRenderable
	{
		[SerializeField]
		public TData data;

		public TItem Item => _item ?? (_item = GetItem());
		public bool IsLocked { get => data.IsLocked; set => data.IsLocked = value; }
		public ItemData ItemData => data;
		public List<MemberInfo> MaterialRefs => _materialRefs ?? (_materialRefs = GetMembersWithAttribute<MaterialReferenceAttribute>());
		public List<MemberInfo> TextureRefs => _textureRefs ?? (_textureRefs = GetMembersWithAttribute<TextureReferenceAttribute>());

		private Table _table;

		protected Table Table => _table ?? (_table = gameObject.transform.GetComponentInParent<TableAuthoring>()?.Item);

		private TItem _item;
		private List<MemberInfo> _materialRefs;
		private List<MemberInfo> _textureRefs;

		private readonly Logger _logger = LogManager.GetCurrentClassLogger();

		// for tracking if we need to rebuild the meshes (handled by the editor scripts) during undo/redo flows
		[HideInInspector]
		[SerializeField]
		private bool _meshDirty;
		public bool MeshDirty { get => _meshDirty; set => _meshDirty = value; }

		private static readonly Color AabbColor = new Color32(255, 0, 252, 255);
		private static readonly Color ColliderColor = new Color32(0, 255, 75, 255);

		public ItemAuthoring<TItem, TData> SetItem(TItem item, string gameObjectName = null)
		{
			_item = item;
			data = item.Data;
			name = gameObjectName ?? data.GetName();
			ItemDataChanged();
			return this;
		}

		public void RebuildMeshes()
		{
			if (data == null) {
				_logger.Warn("Cannot retrieve data component for a {0}.", typeof(TItem).Name);
				return;
			}
			var table = transform.GetComponentInParent<TableAuthoring>();
			if (table == null) {
				_logger.Warn("Cannot retrieve table component from {0}, not updating meshes.", data.GetName());
				return;
			}

			var rog = Item.GetRenderObjects(table.Table, Origin.Original, false);
			var children = Children;
			if (children == null) {
				UpdateMesh(Item.Name, gameObject, rog, table);
			} else {
				foreach (var child in children) {
					if (transform.childCount == 0) {
						//Find the matching  renderObject  and Update it based on base gameObject
						var ro = rog.RenderObjects.FirstOrDefault(r => r.Name == child);
						if (ro != null)
						{
							UpdateMesh(child, gameObject, rog, table);
							break;
						}
					} else {
						Transform childTransform = transform.Find(child);
						if (childTransform != null) {
							UpdateMesh(child, childTransform.gameObject, rog, table);
						} else {
							// child hasn't been created yet (i.e. ramp might have changed type)
							var ro = rog.RenderObjects.FirstOrDefault(r => r.Name == child);
							if (ro != null) {
								var subObj = new GameObject(ro.Name);
								subObj.transform.SetParent(transform, false);
								subObj.layer = VpxConverter.ChildObjectsLayer;
							}
						}
					}
				}
			}
			transform.SetFromMatrix(rog.TransformationMatrix.ToUnityMatrix());
			ItemDataChanged();
			_meshDirty = false;
		}

		protected virtual void ItemDataChanged() {}

		public virtual ItemDataTransformType EditorPositionType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorPosition() { return Vector3.zero; }
		public virtual void SetEditorPosition(Vector3 pos) { }

		public virtual ItemDataTransformType EditorRotationType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorRotation() { return Vector3.zero; }
		public virtual void SetEditorRotation(Vector3 rot) { }

		public virtual ItemDataTransformType EditorScaleType => ItemDataTransformType.None;
		public virtual Vector3 GetEditorScale() { return Vector3.zero; }
		public virtual void SetEditorScale(Vector3 rot) { }

		protected void Convert(Entity entity, EntityManager dstManager)
		{
			Item.Index = entity.Index;
			Item.Version = entity.Version;
		}

		protected virtual void OnDrawGizmos()
		{
			// handle dirty whenever scene view draws just in case a field or dependant changed and our
			// custom inspector window isn't up to process it
			if (_meshDirty) {
				RebuildMeshes();
			}

			// Draw invisible gizmos over top of the sub meshes of this item so clicking in the scene view
			// selects the item itself first, which is most likely what the user would want
			var mfs = GetComponentsInChildren<MeshFilter>();
			Gizmos.color = Color.clear;
			Gizmos.matrix = Matrix4x4.identity;
			foreach (var mf in mfs) {
				Gizmos.DrawMesh(mf.sharedMesh, mf.transform.position, mf.transform.rotation, mf.transform.lossyScale);
			}
		}

		protected virtual void OnDrawGizmosSelected()
		{
			if (PhysicsDebug.ShowAabbs) {
				var ltw = transform.GetComponentInParent<TableAuthoring>().gameObject.transform.localToWorldMatrix;
				if (Item is IHittable hittable) {
					hittable.Init(Table);
					var hits = hittable.GetHitShapes();
					if (hittable != PhysicsDebug.SelectedHittable) {
						PhysicsDebug.SelectedCollider = hits.Length == 1 ? 0 : -1;
						PhysicsDebug.SelectedHittable = hittable;
						PhysicsDebug.OnItemSelected(hittable);
					}

					if (PhysicsDebug.SelectedCollider > -1) {
						hits[PhysicsDebug.SelectedCollider].CalcHitBBox();
						DrawAabb(ltw, hits[PhysicsDebug.SelectedCollider].HitBBox);
						DrawCollider(ltw, hits[PhysicsDebug.SelectedCollider]);

					} else {
						foreach (var hit in hits) {
							hit.CalcHitBBox();
							DrawAabb(ltw, hit.HitBBox);
						}
					}
				}
			}
		}


		private static void UpdateMesh(string childName, GameObject go, RenderObjectGroup rog, TableAuthoring table)
		{
			var mr = go.GetComponent<MeshRenderer>();
			var ro = rog.RenderObjects.FirstOrDefault(r => r.Name == childName);
			if (ro == null || !ro.IsVisible) {
				if (mr != null) {
					mr.enabled = false;
				}
				return;
			}
			var mf = go.GetComponent<MeshFilter>();
			if (mf != null) {
				var unityMesh = mf.sharedMesh;
				ro.Mesh.ApplyToUnityMesh(unityMesh);
			}

			if (mr != null) {
				if (table != null) {
					mr.sharedMaterial = ro.Material.ToUnityMaterial(table);
				}
				mr.enabled = true;
			}
		}

		#region Physics Debug

		private static void DrawAabb(Matrix4x4 ltw, Rect3D aabb)
		{
			var p00 = ltw.MultiplyPoint(new Vector3( aabb.Left, aabb.Top, aabb.ZHigh));
			var p01 = ltw.MultiplyPoint(new Vector3(aabb.Left, aabb.Bottom, aabb.ZHigh));
			var p02 = ltw.MultiplyPoint(new Vector3(aabb.Right, aabb.Bottom, aabb.ZHigh));
			var p03 = ltw.MultiplyPoint(new Vector3(aabb.Right, aabb.Top, aabb.ZHigh));

			var p10 = ltw.MultiplyPoint(new Vector3( aabb.Left, aabb.Top, aabb.ZLow));
			var p11 = ltw.MultiplyPoint(new Vector3(aabb.Left, aabb.Bottom, aabb.ZLow));
			var p12 = ltw.MultiplyPoint(new Vector3(aabb.Right, aabb.Bottom, aabb.ZLow));
			var p13 = ltw.MultiplyPoint(new Vector3(aabb.Right, aabb.Top, aabb.ZLow));

			Gizmos.color = AabbColor;
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

		private void DrawCollider(Matrix4x4 ltw, HitObject hitObject)
		{
			Gizmos.color = ColliderColor;
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
					DrawCollider(ltw, gateHit.LineSeg0);
					DrawCollider(ltw, gateHit.LineSeg1);
					break;
				}

				case SpinnerHit spinnerHit: {
					DrawCollider(ltw, spinnerHit.LineSeg0);
					DrawCollider(ltw, spinnerHit.LineSeg1);
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
					DrawCollider(ltw, plungerHit.LineSegBase);
					DrawCollider(ltw, plungerHit.LineSegEnd);
					DrawCollider(ltw, plungerHit.LineSegSide[0]);
					DrawCollider(ltw, plungerHit.LineSegSide[1]);
					DrawCollider(ltw, plungerHit.JointBase[0]);
					DrawCollider(ltw, plungerHit.JointBase[1]);
					DrawCollider(ltw, plungerHit.JointEnd[0]);
					DrawCollider(ltw, plungerHit.JointEnd[1]);
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


		private List<MemberInfo> GetMembersWithAttribute<TAttr>() where TAttr: Attribute
		{
			List<MemberInfo> members = new List<MemberInfo>();
			foreach (var member in typeof(TData).GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
				if (member.GetCustomAttribute<TAttr>() != null) {
					members.Add(member);
				}
			}
			return members;
		}

		protected abstract string[] Children { get; }

		protected abstract TItem GetItem();

		public string Name { get => Item.Name; set => Item.Name = value; }

		public int EditorLayer { get => data.EditorLayer; set => data.EditorLayer = value; }
		public string EditorLayerName { get => data.EditorLayerName; set => data.EditorLayerName = value; }
		public bool EditorLayerVisibility { get => data.EditorLayerVisibility; set => data.EditorLayerVisibility = value; }
	}

	/// <summary>
	/// The intermediate class through which the physics debug window communicates.
	/// </summary>
	public static class PhysicsDebug
	{
		public static bool ShowAabbs;

		public static IHittable SelectedHittable;

		public static int SelectedCollider;

		public static event EventHandler ItemSelected;

		internal static void OnItemSelected(IHittable hittable)
		{
			ItemSelected?.Invoke(hittable, EventArgs.Empty);
		}
	}
}
