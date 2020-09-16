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

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Base class for VPX-style "Manager" windows, such as the Material Manager
	/// </summary>
	/// <typeparam name="T">class of type IManagerListData that represents the data being edited</typeparam>
	public abstract class ManagerWindow<T> : LockingTableEditorWindow, IHasCustomMenu where T: class, IManagerListData
	{
		protected virtual string DataTypeName => "";
		protected virtual bool DetailsEnabled => true;
		protected virtual float DetailsMaxWidth => 300f;

		protected virtual void OnButtonBarGUI() { }
		protected virtual void OnDataDetailGUI() { }
		protected virtual void RenameExistingItem(T data, string desiredName) { }
		protected virtual List<T> CollectData() => new List<T>();
		protected virtual void OnDataChanged(string undoName, T data) { }
		protected virtual void AddNewData(string undoName, string newName) { }
		protected virtual void RemoveData(string undoName, T data) { }
		protected virtual int MoveData(string undoName, T data, int increment) { return 0; }
		protected virtual void CloneData(string undoName, string newName, T data) { }
		protected virtual void OnDataSelected() { }

		protected virtual bool ListViewItemRendererEnabled => false;
		protected virtual void OnListViewItemRenderer(T data, Rect rect, int column) { }

		protected T _selectedItem;

		private List<T> _data = new List<T>();
		private ManagerListView<T> _listView;
		private TreeViewState _treeViewState;
		private bool _renaming = false;
		private string _renameBuffer = "";
		[SerializeField] private string _forceSelectItemWithName;
		private bool _isImplAddNewData = false;
		private bool _isImplRemoveData = false;
		private bool _isImplCloneData = false;
		private bool _isImplMoveData = false;
		private bool _isImplRenameExistingItem = false;
		private Vector2 _scrollPos = Vector2.zero;

		protected void Reload()
		{
			if (_table != null) {
				_data = CollectData();
				_listView.SetData(_data);
			}
		}

		protected void ResizeToFit()
		{
			if (_table != null) {
				_listView.multiColumnHeader.ResizeToFit();
			}
		}

		protected virtual void OnEnable()
		{
			_isImplAddNewData = IsImplemented("AddNewData");
			_isImplRemoveData = IsImplemented("RemoveData");
			_isImplCloneData = IsImplemented("CloneData");
			_isImplMoveData = IsImplemented("MoveData");
			_isImplRenameExistingItem = IsImplemented("RenameExistingItem");

			// force gui draw when we perform an undo so we see the fields change back
			Undo.undoRedoPerformed -= UndoPerformed;
			Undo.undoRedoPerformed += UndoPerformed;

			if (_treeViewState == null) {
				_treeViewState = new TreeViewState();
			}

			FindTable();
		}

		protected virtual void OnGUI()
		{
			// if the table went away, clear the selected material and list data
			if (_table == null) {
				_selectedItem = null;
				_listView?.SetData(null);
			}

			if (!string.IsNullOrEmpty(_forceSelectItemWithName)) {
				_listView.SelectItemWithName(_forceSelectItemWithName);
				_forceSelectItemWithName = null;
			}

			EditorGUILayout.BeginHorizontal();
			if (_isImplAddNewData && GUILayout.Button("Add", GUILayout.ExpandWidth(false))) {
				// use a serialized field to force list item selection in the next gui pass
				// this way undo will cause it to happen again, and if its no there anymore, just deselect any
				string newDataName = GetUniqueName("New " + DataTypeName);
				string undoName = "Add " + DataTypeName;
				_forceSelectItemWithName = newDataName;
				Undo.RecordObjects(new Object[] { this, _table }, undoName);
				AddNewData(undoName, newDataName);
				Reload();
				SetSelection(_data.Count - 1);
			}
			if (_isImplRemoveData && GUILayout.Button("Remove", GUILayout.ExpandWidth(false)) && _selectedItem != null) {
				if (EditorUtility.DisplayDialog("Delete " + DataTypeName, $"Are you sure want to delete \"{_selectedItem.Name}\"?", "Delete", "Cancel")) {
					string undoName = "Remove " + DataTypeName;
					Undo.RecordObjects(new Object[] { this, _table }, undoName);
					RemoveData(undoName, _selectedItem);
					Reload();
					SetSelection(0);
				}
			}
			if (_isImplCloneData && GUILayout.Button("Clone", GUILayout.ExpandWidth(false)) && _selectedItem != null) {
				string newDataName = GetUniqueName(_selectedItem.Name);
				string undoName = "Clone " + DataTypeName + ": " + _selectedItem.Name;
				_forceSelectItemWithName = newDataName;
				Undo.RecordObjects(new Object[] { this, _table }, undoName);
				CloneData(undoName, newDataName, _selectedItem);
				Reload();
				SetSelection(_data.Count - 1);
			}
			OnButtonBarGUI();
			EditorGUILayout.EndHorizontal();

			if (_isImplMoveData && _selectedItem != null) {
				EditorGUILayout.BeginHorizontal();
				GUILayout.Label("Move ", GUILayout.ExpandWidth(false));
				int moveIncrement = 0;
				if (GUILayout.Button("Top", GUILayout.ExpandWidth(false))) {
					moveIncrement = -_data.Count;
				}
				if (GUILayout.Button("Up", GUILayout.ExpandWidth(false))) {
					moveIncrement = -1;
				}
				if (GUILayout.Button("Down", GUILayout.ExpandWidth(false))) {
					moveIncrement = 1;
				}
				if (GUILayout.Button("Bottom", GUILayout.ExpandWidth(false))) {
					moveIncrement = _data.Count;
				}
				if (moveIncrement != 0) {
					string undoName = "Move " + DataTypeName;
					Undo.RecordObjects(new Object[] { this, _table }, undoName);
					int newIdx = MoveData(undoName, _selectedItem, moveIncrement);
					SetSelection(newIdx);
					Reload();
				}
				EditorGUILayout.EndHorizontal();
			}

			EditorGUILayout.BeginHorizontal();

			// list
			GUILayout.FlexibleSpace();
			var r = GUILayoutUtility.GetLastRect();
			var listRect = new Rect(r.x, r.y, r.width, position.height - r.y);
			_listView?.OnGUI(listRect);

			if (DetailsEnabled)
			{
				// options
				EditorGUILayout.BeginVertical(GUILayout.MaxWidth(DetailsMaxWidth));
				if (_selectedItem != null)
				{
					EditorGUILayout.BeginHorizontal();
					if (_renaming)
					{
						_renameBuffer = EditorGUILayout.TextField(_renameBuffer);
						if (GUILayout.Button("Save"))
						{
							string newName = GetUniqueName(_renameBuffer, _selectedItem);
							if (!string.IsNullOrEmpty(newName))
							{
								RenameExistingItem(_selectedItem, newName);
							}
							_renaming = false;
							Reload();
						}
						if (GUILayout.Button("Cancel"))
						{
							_renaming = false;
							GUI.FocusControl(""); // de-focus on cancel because unity will retain previous buffer text until focus changes
						}
					}
					else
					{
						EditorGUILayout.LabelField(_selectedItem.Name);
						if (_isImplRenameExistingItem && GUILayout.Button("Rename"))
						{
							_renaming = true;
							_renameBuffer = _selectedItem.Name;
						}
					}
					EditorGUILayout.EndHorizontal();

					_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
					OnDataDetailGUI();
					EditorGUILayout.EndScrollView();
				}
				else
				{
					EditorGUILayout.LabelField("Nothing selected");
				}
				EditorGUILayout.EndVertical();
			}

			EditorGUILayout.EndHorizontal();
		}

		protected void FloatField(string label, ref float field)
		{
			EditorGUI.BeginChangeCheck();
			float val = EditorGUILayout.FloatField(label, field);
			if (EditorGUI.EndChangeCheck()) {
				FinalizeChange(label, ref field, val);
			}
		}

		protected void SliderField(string label, ref float field, float min = 0f, float max = 1f, string tooltip = "")
		{
			EditorGUI.BeginChangeCheck();
			float val = EditorGUILayout.Slider(new GUIContent(label, tooltip), field, min, max);
			if (EditorGUI.EndChangeCheck()) {
				FinalizeChange(label, ref field, val);
			}
		}

		protected void SliderField(string label, ref int field, int min = 0, int max = 1, string tooltip = "")
		{
			EditorGUI.BeginChangeCheck();
			int val = EditorGUILayout.IntSlider(new GUIContent(label, tooltip), field, min, max);
			if (EditorGUI.EndChangeCheck()) {
				FinalizeChange(label, ref field, val);
			}
		}

		protected void ToggleField(string label, ref bool field, string tooltip = "")
		{
			EditorGUI.BeginChangeCheck();
			bool val = EditorGUILayout.Toggle(new GUIContent(label, tooltip), field);
			if (EditorGUI.EndChangeCheck()) {
				FinalizeChange(label, ref field, val);
			}
		}

		protected void ColorField(string label, ref Engine.Math.Color field, string tooltip = "")
		{
			EditorGUI.BeginChangeCheck();
			Engine.Math.Color val = EditorGUILayout.ColorField(new GUIContent(label, tooltip), field.ToUnityColor()).ToEngineColor();
			if (EditorGUI.EndChangeCheck()) {
				FinalizeChange(label, ref field, val);
			}
		}

		protected void DropDownField<TField>(string label, ref TField field, string[] optionStrings, TField[] optionValues) where TField : System.IEquatable<TField>
		{
			if (optionStrings == null || optionValues == null || optionStrings.Length != optionValues.Length) {
				return;
			}

			int selectedIndex = 0;
			for (int i = 0; i < optionValues.Length; i++) {
				if (optionValues[i].Equals(field)) {
					selectedIndex = i;
					break;
				}
			}
			EditorGUI.BeginChangeCheck();
			selectedIndex = EditorGUILayout.Popup(label, selectedIndex, optionStrings);
			if (EditorGUI.EndChangeCheck() && selectedIndex >= 0 && selectedIndex < optionValues.Length) {
				FinalizeChange(label, ref field, optionValues[selectedIndex]);
			}
		}

		protected void FinalizeChange<TField>(string label, ref TField field, TField val)
		{
			string undoName = "Edit " + DataTypeName + ": " + label;
			OnDataChanged(undoName, _selectedItem);
			Undo.RecordObject(_table, undoName);
			field = val;
			SceneView.RepaintAll();
		}

		protected string GetMemberValue(MemberInfo mi, object instance)
		{
			switch (mi) {
				case FieldInfo fi: return fi.GetValue(instance) as string;
				case PropertyInfo pi: return pi.GetValue(instance) as string;
			}
			return null;
		}

		protected bool IsReferenced(List<MemberInfo> mis, object instance, string refName)
		{
			if (mis == null) { return false; }
			string refNameLower = refName.ToLower();
			foreach (var mi in mis) {
				if (GetMemberValue(mi, instance)?.ToLower() == refNameLower) {
					return true;
				}
			}
			return false;
		}

		protected void RenameReflectedFields(string undoName, IEditableItemAuthoring item, List<MemberInfo> mis, string oldName, string newName)
		{
			foreach (var mi in mis) {
				string fieldVal = GetMemberValue(mi, item.ItemData);
				if (fieldVal == oldName) {
					Undo.RecordObject(item as Object, undoName);
					switch (mi) {
						case FieldInfo fi: fi.SetValue(item.ItemData, newName); break;
						case PropertyInfo pi: pi.SetValue(item.ItemData, newName); break;
					}
				}
			}
		}

		protected virtual void UndoPerformed()
		{
			Reload();
		}

		private void ItemSelected(List<T> selectedItems)
		{
			_selectedItem = null;
			if (selectedItems.Count > 0) {
				_selectedItem = selectedItems[0]; // not supporting multi select for now
				_renaming = false;
				OnDataSelected();
			}
			Repaint();
		}

		protected override void SetTable(TableAuthoring table)
		{
			_data.Clear();
			if (_table != null) {
				_data = CollectData();
			}

			if (ListViewItemRendererEnabled)
			{
				_listView = new ManagerListView<T>(_treeViewState, _data, OnListViewItemRenderer, ItemSelected);
			}
			else
			{
				_listView = new ManagerListView<T>(_treeViewState, _data, null, ItemSelected);
			}
		}

		private bool IsNameInUse(string name, T ignore = null)
		{
			foreach (var item in _data) {
				if (item != ignore && name.ToLower() == item.Name.ToLower()) {
					return true;
				}
			}
			return false;
		}

		private string GetUniqueName(string desiredName, T ignore = null)
		{
			string acceptedName = desiredName;
			int appendNum = 1;
			while (IsNameInUse(acceptedName, ignore)) {
				acceptedName = desiredName + appendNum;
				appendNum++;
			}
			return acceptedName;
		}

		protected void SetSelection(int idx)
		{
			_listView.SetSelection(new int[] { idx }.ToList());
		}

		// check is a concrete class implements the given method name
		private bool IsImplemented(string methodName)
		{
			var mi = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			return mi != null && mi.GetBaseDefinition().DeclaringType != mi.DeclaringType;
		}
	}
}
