using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.VPT.Surface;
using VisualPinball.Unity.VPT.Table;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.Editor.Inspectors
{
	public abstract class ItemInspector : UnityEditor.Editor
    {
		protected TableBehavior _table;
		protected SurfaceBehavior _surface;

		protected virtual void OnEnable()
		{
			_table = (target as MonoBehaviour).gameObject.GetComponentInParent<TableBehavior>();
		}

		public override void OnInspectorGUI()
		{
			var item = target as IEditableItemBehavior;
			if (item == null) return;

			GUILayout.Space(10);
			if( GUILayout.Button( "Force Update Mesh" ) ) {
				item.MeshDirty = true;
			}

			if (item.MeshDirty) {
				item.RebuildMeshes();
			}
		}

		protected void ItemDataField(string label, ref float field, bool dirtyMesh = true)
		{
			EditorGUI.BeginChangeCheck();
			float val = EditorGUILayout.FloatField(label, field);
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				field = val;
			}
		}

		protected void ItemDataField(string label, ref int field, bool dirtyMesh = true)
		{
			EditorGUI.BeginChangeCheck();
			int val = EditorGUILayout.IntField(label, field);
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				field = val;
			}
		}

		protected void ItemDataField(string label, ref string field, bool dirtyMesh = true)
		{
			EditorGUI.BeginChangeCheck();
			string val = EditorGUILayout.TextField(label, field);
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				field = val;
			}
		}

		protected void ItemDataField(string label, ref bool field, bool dirtyMesh = true)
		{
			EditorGUI.BeginChangeCheck();
			bool val = EditorGUILayout.Toggle(label, field);
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				field = val;
			}
		}

		protected void ItemDataField(string label, ref Vertex2D field, bool dirtyMesh = true)
		{
			EditorGUI.BeginChangeCheck();
			Vertex2D val = EditorGUILayout.Vector2Field(label, field.ToUnityVector2()).ToVertex2D();
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				field = val;
			}
		}

		protected void ItemDataField(string label, ref Vertex3D field, bool dirtyMesh = true)
		{
			EditorGUI.BeginChangeCheck();
			Vertex3D val = EditorGUILayout.Vector3Field(label, field.ToUnityVector3()).ToVertex3D();
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				field = val;
			}
		}

		protected void SurfaceField(string label, ref string field, bool dirtyMesh = true)
		{
			if (_surface?.name != field) {
				_surface = null;
			}

			var mb = target as MonoBehaviour;
			if (_surface == null) {
				string currentFieldName = field;
				if (currentFieldName != null && _table.Table.Surfaces.ContainsKey(currentFieldName)) {
					_surface = _table.gameObject.GetComponentsInChildren<SurfaceBehavior>(true)
						.FirstOrDefault(s => s.name == currentFieldName);
				}
			}

			EditorGUI.BeginChangeCheck();
			_surface = (SurfaceBehavior)EditorGUILayout.ObjectField(label, _surface, typeof(SurfaceBehavior), true);
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				field = _surface != null ? _surface.name : "";
			}
		}

		private void FinishEdit(string label, bool dirtyMesh = true)
		{
			string undoLabel = "Edit " + label;
			if (dirtyMesh) {
				// set dirty flag true before recording object state for the undo so meshes will rebuild after the undo as well
				var item = (target as IEditableItemBehavior);
				if (item != null) {
					item.MeshDirty = true;
					Undo.RecordObject(this, undoLabel);
				}
			}
			Undo.RecordObject(target, undoLabel);
		}
	}
}
