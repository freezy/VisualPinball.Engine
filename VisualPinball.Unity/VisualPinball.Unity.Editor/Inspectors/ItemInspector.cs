using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.VPT.Surface;
using VisualPinball.Unity.VPT.Table;
using VisualPinball.Unity.Extensions;
using System;
using System.Collections.Generic;
using VisualPinball.Unity.Editor.Utils;

namespace VisualPinball.Unity.Editor.Inspectors
{
	public abstract class ItemInspector : UnityEditor.Editor
    {
		protected TableBehavior _table;
		protected SurfaceBehavior _surface;

		protected string[] _allMaterials = new string[0];
		protected string[] _allTextures = new string[0];

		protected virtual void OnEnable()
		{
			_table = (target as MonoBehaviour)?.gameObject.GetComponentInParent<TableBehavior>();

			if (_table != null) {
				if (_table.data.Materials != null) {
					_allMaterials = new string[_table.data.Materials.Length + 1];
					_allMaterials[0] = "- none -";
					for (int i = 0; i < _table.data.Materials.Length; i++) {
						_allMaterials[i + 1] = _table.data.Materials[i].Name;
					}
					Array.Sort(_allMaterials, 1, _allMaterials.Length - 1);
				}
				if (_table.Textures != null) {
					_allTextures = new string[_table.Textures.Length + 1];
					_allTextures[0] = "- none -";
					for (int i = 0; i < _table.Textures.Length; i++) {
						_allTextures[i + 1] = _table.Textures[i].Name;
					}
					Array.Sort(_allTextures, 1, _allTextures.Length - 1);
				}
			}
		}

		protected void OnPreInspectorGUI()
		{
			var item = (target as IEditableItemBehavior);
			if (item == null) return;

			//Cannot use DataFieldUtils there because item.IsLocked cannot be a ref;
			EditorGUI.BeginChangeCheck();
			bool newLock = EditorGUILayout.Toggle("IsLocked", item.IsLocked);
			if (EditorGUI.EndChangeCheck())
			{
				string message = "";
				List<UnityEngine.Object> recordObjs = new List<UnityEngine.Object>();
				if (FinishEdit("IsLocked", out message, recordObjs, ("redrawScene",true)))
				{
					Undo.RecordObjects(recordObjs.ToArray(), $"{message}");
				}
				item.IsLocked = newLock;
			}
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

		protected void SurfaceField(string label, ref string field, bool dirtyMesh = true)
		{
			if (_surface?.name != field) {
				_surface = null;
			}

			var mb = target as MonoBehaviour;
			if (_surface == null && _table != null) {
				string currentFieldName = field;
				if (currentFieldName != null && _table.Table.Surfaces.ContainsKey(currentFieldName)) {
					_surface = _table.gameObject.GetComponentsInChildren<SurfaceBehavior>(true)
						.FirstOrDefault(s => s.name == currentFieldName);
				}
			}


			_surface = (SurfaceBehavior)DataFieldUtils.ItemObjectField(label, _surface, true, FinishEdit);
			field = _surface != null ? _surface.name : "";
		}

		protected void MaterialField(string label, ref string field, bool dirtyMesh = true)
		{
			DataFieldUtils.DropDownField(label, ref field, _allMaterials, _allMaterials, FinishEdit, ("dirtyMesh", (object)dirtyMesh));
			if (_allMaterials.Length > 0 && field == _allMaterials[0]) {
				field = ""; // don't store the none value string in our data
			}
		}

		protected bool FinishEdit(string label, out string message, List<UnityEngine.Object> recordObjs, params (string, object)[] pList)
		{
			bool dirtyMesh = Enumerable.Count<(string, object)>(pList, pair => pair.Item1 == "dirtyMesh") > 0 ? (bool)Enumerable.First<(string, object)>(pList, pair => pair.Item1 == "dirtyMesh").Item2 : true;
			bool redrawScene = Enumerable.Count<(string, object)>(pList, pair => pair.Item1 == "redrawScene") > 0 ? (bool)Enumerable.First<(string, object)>(pList, pair => pair.Item1 == "redrawScene").Item2 : false;

			message = $"[{target?.name}] Edit {label}";
			if (dirtyMesh)
			{
				// set dirty flag true before recording object state for the undo so meshes will rebuild after the undo as well
				var item = (target as IEditableItemBehavior);
				if (item != null)
				{
					item.MeshDirty = true;
					recordObjs.Add(this);
				}
			}
			recordObjs.Add(target);
			if (redrawScene)
			{
				SceneView.RepaintAll();
			}
			return true;
		}
	}
}
