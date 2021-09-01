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
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Color = VisualPinball.Engine.Math.Color;
using Object = UnityEngine.Object;

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
		protected virtual bool SetupCompleted() => true;

		protected virtual bool ListViewItemRendererEnabled => false;
		protected virtual void OnListViewItemRenderer(T data, Rect rect, int column) { }
		protected float RowHeight {
			get => _rowHeight;
			set {
				_rowHeight = value;
				if (_listView != null) {
					_listView.RowHeight = value;
				}
			}
		}

		protected bool IsTableSelected => _listView != null && _tableAuthoring != null;

		private float _rowHeight;

		protected T _selectedItem;

		private List<T> _data = new List<T>();
		private ManagerListView<T> _listView;
		private TreeViewState _treeViewState;
		private bool _renaming;
		private string _renameBuffer = string.Empty;
		[SerializeField] private string _forceSelectItemWithName;
		private bool _isImplAddNewData;
		private bool _isImplRemoveData;
		private bool _isImplCloneData;
		private bool _isImplMoveData;
		private bool _isImplRenameExistingItem;
		private Vector2 _scrollPos = Vector2.zero;

		public void Reload()
		{
			if (_tableAuthoring) {
				_data = CollectData();
				_listView.SetData(_data);
			}
		}

		public override void OnEnable()
		{
			base.OnEnable();

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

			SetTable(TableSelector.Instance.SelectedTable);
		}

		protected virtual void OnGUI()
		{
			// if the table went away, clear the selected material and list data
			if (_tableAuthoring == null) {
				_selectedItem = null;
				_listView?.SetData(null);
			}

			if (!SetupCompleted()) {
				return;
			}

			if (!string.IsNullOrEmpty(_forceSelectItemWithName)) {
				_listView.SelectItemWithName(_forceSelectItemWithName);
				_forceSelectItemWithName = null;
			}

			// shift+enter adds a new item
			if (focusedWindow == this
			    && Event.current.keyCode == KeyCode.Return
			    && Event.current.modifiers == EventModifiers.Shift
			    && Event.current.type == EventType.KeyDown)
			{
				Add();
			}

			EditorGUILayout.BeginHorizontal();
			EditorGUI.BeginDisabledGroup(!IsTableSelected);
			if (_isImplAddNewData && GUILayout.Button("Add", GUILayout.ExpandWidth(false))) {
				Add();
			}
			if (_isImplRemoveData && GUILayout.Button("Remove", GUILayout.ExpandWidth(false)) && _selectedItem != null) {

				List<T> selectedData = _listView.GetSelectedData();

				string text = $"Are you sure want to delete \"{_selectedItem.Name}\"?";
				if ( selectedData.Count > 1)
				{
					text = $"Are you sure want to delete the {selectedData.Count} selected rows?";
				}

				if (EditorUtility.DisplayDialog("Delete " + DataTypeName, text, "Delete", "Cancel")) {
					Delete( selectedData);
				}
			}
			if (_isImplCloneData && GUILayout.Button("Clone", GUILayout.ExpandWidth(false)) && _selectedItem != null) {
				Clone();
			}
			OnButtonBarGUI();
			EditorGUI.EndDisabledGroup();
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
					Undo.RecordObjects(new Object[] { this, _tableAuthoring }, undoName);
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

		protected void Add()
		{
			// use a serialized field to force list item selection in the next gui pass
			// this way undo will cause it to happen again, and if its no there anymore, just deselect any
			string newDataName = GetUniqueName("New " + DataTypeName);
			string undoName = "Add " + DataTypeName;
			_forceSelectItemWithName = newDataName;
			Undo.RecordObjects(new Object[] { this, _tableAuthoring }, undoName);
			AddNewData(undoName, newDataName);
			Reload();
			SetSelection(_data.Count - 1);
		}

		private void Clone()
		{
			string newDataName = GetUniqueName(_selectedItem.Name);
			string undoName = "Clone " + DataTypeName + ": " + _selectedItem.Name;
			_forceSelectItemWithName = newDataName;
			Undo.RecordObjects(new Object[] { this, _tableAuthoring }, undoName);
			CloneData(undoName, newDataName, _selectedItem);
			Reload();
			SetSelection(_data.Count - 1);
		}

		private void Delete(List<T> selectedData)
		{
			string undoName = "Remove " + DataTypeName;
			Undo.RecordObjects(new Object[] { this, _tableAuthoring }, undoName);

			foreach( T data in selectedData)
			{
				RemoveData(undoName, data);
			}

			Reload();
			SetSelection(0);
		}

		#region Fields

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

		protected void ColorField(string label, ref Color field, string tooltip = "")
		{
			EditorGUI.BeginChangeCheck();
			Color val = EditorGUILayout.ColorField(new GUIContent(label, tooltip), field.ToUnityColor()).ToEngineColor();
			if (EditorGUI.EndChangeCheck()) {
				FinalizeChange(label, ref field, val);
			}
		}

		protected void DropDownField<TField>(string label, ref TField field, string[] optionStrings, TField[] optionValues) where TField : IEquatable<TField>
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

		#endregion

		protected void FinalizeChange<TField>(string label, ref TField field, TField val)
		{
			string undoName = "Edit " + DataTypeName + ": " + label;
			OnDataChanged(undoName, _selectedItem);
			Undo.RecordObject(_tableAuthoring, undoName);
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
			if (_tableAuthoring != null) {
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

			_listView.RowHeight = _rowHeight;
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
