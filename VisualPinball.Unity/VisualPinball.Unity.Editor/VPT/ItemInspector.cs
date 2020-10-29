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
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity.Editor
{
	public abstract class ItemInspector : UnityEditor.Editor
	{
		public abstract MonoBehaviour UndoTarget { get; }

		protected TableAuthoring _table;

		private Dictionary<string, MonoBehaviour> _refItems = new Dictionary<string, MonoBehaviour>();

		private string[] _allMaterials = new string[0];
		private string[] _allTextures = new string[0];

		public static event Action<IIdentifiableItemAuthoring, string, string> ItemRenamed;

		#region Unity Events

		protected virtual void OnEnable()
		{

// #if UNITY_EDITOR
// 			// for convenience move item behavior to the top of the list
// 			// we're opting to due this here as opposed to at import time since modifying objects
// 			// in this way caused them to not be part of the created object undo stack
// 			if (target != null && target is MonoBehaviour mb) {
// 				var numComp = mb.GetComponents<MonoBehaviour>().Length;
// 				if (mb is IItemColliderAuthoring || mb is IItemMeshAuthoring || mb is IItemMovementAuthoring) {
// 					numComp--;
// 				}
// 				for (var i = 0; i <= numComp; i++) {
// 					UnityEditorInternal.ComponentUtility.MoveComponentUp(mb);
// 				}
// 			}
// #endif

			_table = (target as MonoBehaviour)?.gameObject.GetComponentInParent<TableAuthoring>();
			PopulateDropDownOptions();
		}

		protected virtual void OnDisable()
		{
		}

		public override void OnInspectorGUI()
		{
			if (!(target is IItemMainAuthoring item)) {
				return;
			}

			GUILayout.Space(10);
			if (GUILayout.Button("Force Update Mesh")) {
				item.SetMeshDirty();
			}

			item.RebuildMeshIfDirty();
		}

		#endregion

		private void PopulateDropDownOptions()
		{
			if (_table == null) return;

			if (_table.Data.Materials != null) {
				_allMaterials = new string[_table.Data.Materials.Length + 1];
				_allMaterials[0] = "- none -";
				for (var i = 0; i < _table.Data.Materials.Length; i++) {
					_allMaterials[i + 1] = _table.Data.Materials[i].Name;
				}
				Array.Sort(_allMaterials, 1, _allMaterials.Length - 1);
			}
			if (_table.Textures != null) {
				_allTextures = new string[_table.Textures.Count + 1];
				_allTextures[0] = "- none -";
				_table.Textures.Select(tex => tex.Name).ToArray().CopyTo(_allTextures, 1);
				Array.Sort(_allTextures, 1, _allTextures.Length - 1);
			}
		}

		private void OnHierarchyChange()
		{
			if (target is MonoBehaviour bh && target is IIdentifiableItemAuthoring item && bh != null) {
				var go = bh.gameObject;
				if (item.Name != go.name) {
					var oldName = item.Name;
					item.Name = go.name;
					ItemRenamed?.Invoke(item, oldName, go.name);
				}
			}
		}

		protected void OnPreInspectorGUI()
		{
			if (!(target is IItemMainAuthoring item)) {
				return;
			}

			EditorGUI.BeginChangeCheck();
			var val = EditorGUILayout.TextField("Name", item.ItemData.GetName());
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit("Name", false);
				item.ItemData.SetName(val);
			}

			EditorGUI.BeginChangeCheck();
			var newLock = EditorGUILayout.Toggle("IsLocked", item.IsLocked);
			if (EditorGUI.EndChangeCheck())
			{
				FinishEdit("IsLocked");
				item.IsLocked = newLock;
				SceneView.RepaintAll();
			}

			if (target is IIdentifiableItemAuthoring identity && target is MonoBehaviour bh) {
				if (identity.Name != bh.gameObject.name) {
					var oldName = identity.Name;
					identity.Name = bh.gameObject.name;
					ItemRenamed?.Invoke(identity, oldName, bh.gameObject.name);
				}
			}
		}

		#region Data Fields

		protected void ItemDataField(string label, ref float field, bool dirtyMesh = true, Action<float, float> onChanged = null)
		{
			EditorGUI.BeginChangeCheck();
			var val = EditorGUILayout.FloatField(label, field);
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				var fieldBefore = field;
				field = val;
				onChanged?.Invoke(fieldBefore, field);
			}
		}

		public void ItemDataSlider(string label, ref float field, float leftVal, float rightVal, bool dirtyMesh = true)
		{
			EditorGUI.BeginChangeCheck();
			var val = EditorGUILayout.Slider(label, field, leftVal, rightVal);
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				field = val;
			}
		}

		protected void ItemDataField(string label, ref int field, bool dirtyMesh = true)
		{
			EditorGUI.BeginChangeCheck();
			var val = EditorGUILayout.IntField(label, field);
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				field = val;
			}
		}

		public void ItemDataSlider(string label, ref int field, int leftVal, int rightVal, bool dirtyMesh = true)
		{
			EditorGUI.BeginChangeCheck();
			var val = EditorGUILayout.IntSlider(label, field, leftVal, rightVal);
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				field = val;
			}
		}

		protected void ItemDataField(string label, ref string field, bool dirtyMesh = true)
		{
			EditorGUI.BeginChangeCheck();
			var val = EditorGUILayout.TextField(label, field);
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				field = val;
			}
		}

		protected void ItemDataField(string label, ref bool field, bool dirtyMesh = true, Action<bool, bool> onChanged = null)
		{
			EditorGUI.BeginChangeCheck();
			var val = EditorGUILayout.Toggle(label, field);
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				var fieldBefore = field;
				field = val;
				onChanged?.Invoke(fieldBefore, field);
			}
		}

		protected void ItemDataField(string label, ref Vertex2D field, bool dirtyMesh = true)
		{
			EditorGUI.BeginChangeCheck();
			var val = EditorGUILayout.Vector2Field(label, field.ToUnityVector2()).ToVertex2D();
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				field = val;
			}
		}

		protected void ItemDataField(string label, ref Vertex3D field, bool dirtyMesh = true)
		{
			EditorGUI.BeginChangeCheck();
			var val = EditorGUILayout.Vector3Field(label, field.ToUnityVector3()).ToVertex3D();
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				field = val;
			}
		}

		protected void ItemDataField(string label, ref Engine.Math.Color field, bool dirtyMesh = true)
		{
			EditorGUI.BeginChangeCheck();
			var val = EditorGUILayout.ColorField(label, field.ToUnityColor()).ToEngineColor();
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				field = val;
			}
		}

		protected void ItemReferenceField<TItemAuthoring, TItem, TData>(string label, string cacheKey, ref string field, bool dirtyMesh = true)
			where TItemAuthoring : ItemAuthoring<TItem, TData>
			where TData : ItemData where TItem : Item<TData>, IRenderable
		{
			if (!_refItems.ContainsKey(cacheKey) && _table != null) {
				var currentFieldName = field;
				if (currentFieldName != null && _table.Table.Has<TItem>(currentFieldName)) {
					_refItems[cacheKey] = _table.gameObject.GetComponentsInChildren<TItemAuthoring>(true)
						.FirstOrDefault(s => s.name == currentFieldName);
				}
			}

			EditorGUI.BeginChangeCheck();
			_refItems[cacheKey] = (TItemAuthoring)EditorGUILayout.ObjectField(label, _refItems.ContainsKey(cacheKey) ? _refItems[cacheKey] : null, typeof(TItemAuthoring), true);
			if (EditorGUI.EndChangeCheck()) {
				FinishEdit(label, dirtyMesh);
				field = _refItems[cacheKey] != null ? _refItems[cacheKey].name : string.Empty;
			}
		}

		protected void SurfaceField(string label, ref string field, bool dirtyMesh = true)
		{
			ItemReferenceField<SurfaceAuthoring, Surface, SurfaceData>(label, "surface", ref field, dirtyMesh);
		}

		protected void DropDownField<T>(string label, ref T field, string[] optionStrings, T[] optionValues, bool dirtyMesh = true, Action<T, T> onChanged = null) where T : IEquatable<T>
		{
			if (optionStrings == null || optionValues == null || optionStrings.Length != optionValues.Length) {
				return;
			}

			var selectedIndex = 0;
			for (var i = 0; i < optionValues.Length; i++) {
				if (optionValues[i].Equals(field)) {
					selectedIndex = i;
					break;
				}
			}
			EditorGUI.BeginChangeCheck();
			selectedIndex = EditorGUILayout.Popup(label, selectedIndex, optionStrings);
			if (EditorGUI.EndChangeCheck() && selectedIndex >= 0 && selectedIndex < optionValues.Length) {
				FinishEdit(label, dirtyMesh);
				var fieldBefore = field;
				field = optionValues[selectedIndex];
				onChanged?.Invoke(fieldBefore, field);
			}
		}

		protected void TextureField(string label, ref string field, bool dirtyMesh = true)
		{
			if (_table == null) return;

			// if the field is set, but the tex isn't in our list, maybe it was added after this
			// inspector was instantiated, so re-grab our options from the table data
			if (!string.IsNullOrEmpty(field) && !_allTextures.Contains(field)) {
				PopulateDropDownOptions();
			}

			var selectedIndex = 0;
			for (var i = 0; i < _allTextures.Length; i++) {
				if (string.Equals(_allTextures[i], field, StringComparison.CurrentCultureIgnoreCase)) {
					selectedIndex = i;
					break;
				}
			}
			EditorGUI.BeginChangeCheck();
			selectedIndex = EditorGUILayout.Popup(label, selectedIndex, _allTextures);
			if (EditorGUI.EndChangeCheck() && selectedIndex >= 0 && selectedIndex < _allTextures.Length) {
				FinishEdit(label, dirtyMesh);
				field = selectedIndex == 0 ? string.Empty : _allTextures[selectedIndex];
			}
		}

		protected void MaterialField(string label, ref string field, bool dirtyMesh = true)
		{
			// if the field is set, but the material isn't in our list, maybe it was added after this
			// inspector was instantiated, so re-grab our mat options from the table data
			if (!string.IsNullOrEmpty(field) && !_allMaterials.Contains(field)) {
				PopulateDropDownOptions();
			}

			DropDownField(label, ref field, _allMaterials, _allMaterials, dirtyMesh);
			if (_allMaterials.Length > 0 && field == _allMaterials[0]) {
				field = string.Empty; // don't store the none value string in our data
			}
		}

		#endregion

		protected virtual void FinishEdit(string label, bool dirtyMesh = true)
		{
			var undoLabel = $"Edit {label} of {target?.name}";
			if (dirtyMesh) {
				// set dirty flag true before recording object state for the undo so meshes will rebuild after the undo as well
				switch (target) {

					case IItemMeshAuthoring meshItem:
						meshItem.IMainAuthoring.SetMeshDirty();
						Undo.RecordObject(UndoTarget, undoLabel);
						break;

					case IItemColliderAuthoring colliderItem:
						colliderItem.MainAuthoring.SetMeshDirty();
						Undo.RecordObject(UndoTarget, undoLabel);
						break;

					case IItemMainAuthoring mainItem:
						mainItem.SetMeshDirty();
						Undo.RecordObject(UndoTarget, undoLabel);
						break;
				}
				EditorUtility.SetDirty(target);
			}
			Undo.RecordObject(UndoTarget, undoLabel);
		}
	}
}
