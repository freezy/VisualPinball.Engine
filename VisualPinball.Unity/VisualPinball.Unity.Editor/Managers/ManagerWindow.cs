using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Editor.Managers
{
	/// <summary>
	/// Base class for VPX-style "Manager" windows, such as the Material Manager
	/// </summary>
	/// <typeparam name="T">class of type IManagerListData that represents the data being edited</typeparam>
	public abstract class ManagerWindow<T> : EditorWindow where T: class, IManagerListData
	{
		protected virtual string DataTypeName => "";

		protected virtual void OnDataDetailGUI() { }
		protected virtual void RenameExistingItem(T data, string desiredName) { }
		protected virtual void CollectData(List<T> data) { }
		protected virtual void OnDataChanged(string undoName, T data) { }
		protected virtual void AddNewData(string newName) { }
		protected virtual void RemoveData(T data) { }
		protected virtual void CloneData(string newName, T data) { }

		protected TableBehavior _table;
		protected T _selectedItem;

		private List<T> _data = new List<T>();
		private ManagerListView<T> _listView;
		private TreeViewState _treeViewState;
		private bool _renaming = false;
		private string _renameBuffer = "";
		[SerializeField] private string _forceSelectItemWithName;

		protected void Reload()
		{
			_data.Clear();
			CollectData(_data);
			_listView.SetData(_data);
		}

		protected virtual void OnEnable()
		{
			// force gui draw when we perform an undo so we see the fields change back
			Undo.undoRedoPerformed -= UndoPerformed;
			Undo.undoRedoPerformed += UndoPerformed;

			if (_treeViewState == null) {
				_treeViewState = new TreeViewState();
			}

			FindTable();
		}

		protected virtual void OnHierarchyChange()
		{
			// if we don't have a table, look for one when stuff in the scene changes
			if (_table == null) {
				FindTable();
			}
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
			if (IsImplemented("AddNewData") && GUILayout.Button("Add", GUILayout.ExpandWidth(false))) {
				// use a serialized field to force list item selection in the next gui pass
				// this way undo will cause it to happen again, and if its no there anymore, just deselect any
				string newDataName = GetUniqueName("New " + DataTypeName);
				string undoName = "Add " + DataTypeName;
				_forceSelectItemWithName = newDataName;
				Undo.RecordObjects(new Object[] { this, _table }, undoName);
				AddNewData(newDataName);
				Reload();
			}
			if (IsImplemented("RemoveData") && GUILayout.Button("Remove", GUILayout.ExpandWidth(false)) && _selectedItem != null) {
				if (EditorUtility.DisplayDialog("Delete " + DataTypeName, $"Are you sure want to delete \"{_selectedItem.Name}\"?", "Delete", "Cancel")) {
					Undo.RecordObjects(new Object[] { this, _table }, "Remove " + DataTypeName);
					RemoveData(_selectedItem);
					_selectedItem = null;
					Reload();
				}
			}
			if (IsImplemented("CloneData") && GUILayout.Button("Clone", GUILayout.ExpandWidth(false)) && _selectedItem != null) {
				string newDataName = GetUniqueName(_selectedItem.Name);
				string undoName = "Clone " + DataTypeName + ": " + _selectedItem.Name;
				_forceSelectItemWithName = newDataName;
				Undo.RecordObjects(new Object[] { this, _table }, undoName);
				CloneData(newDataName, _selectedItem);
				Reload();
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();

			// list
			GUILayout.FlexibleSpace();
			var r = GUILayoutUtility.GetLastRect();
			var listRect = new Rect(r.x, r.y, r.width, position.height - r.y);
			_listView?.OnGUI(listRect);

			// options
			EditorGUILayout.BeginVertical(GUILayout.MaxWidth(300));
			if (_selectedItem != null) {
				EditorGUILayout.BeginHorizontal();
				if (_renaming) {
					_renameBuffer = EditorGUILayout.TextField(_renameBuffer);
					if (GUILayout.Button("Save")) {
						string newName = GetUniqueName(_renameBuffer, _selectedItem);
						if (!string.IsNullOrEmpty(newName)) {
							RenameExistingItem(_selectedItem, newName);
						}
						_renaming = false;
						Reload();
					}
					if (GUILayout.Button("Cancel")) {
						_renaming = false;
						GUI.FocusControl(""); // de-focus on cancel because unity will retain previous buffer text until focus changes
					}
				} else {
					EditorGUILayout.LabelField(_selectedItem.Name);
					if (IsImplemented("RenameExistingItem") && GUILayout.Button("Rename")) {
						_renaming = true;
						_renameBuffer = _selectedItem.Name;
					}
				}
				EditorGUILayout.EndHorizontal();

				OnDataDetailGUI();
			} else {
				EditorGUILayout.LabelField("Nothing selected");
			}
			EditorGUILayout.EndVertical();

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

		private void UndoPerformed()
		{
			Reload();
		}

		private void ItemSelected(List<T> selectedItems)
		{
			_selectedItem = null;
			if (selectedItems.Count > 0) {
				_selectedItem = selectedItems[0]; // not supporting multi select for now
				_renaming = false;
			}
			Repaint();
		}

		private void FindTable()
		{
			_table = GameObject.FindObjectOfType<TableBehavior>();
			_data.Clear();
			if (_table != null) {
				CollectData(_data);
			}
			_listView = new ManagerListView<T>(_treeViewState, _data, ItemSelected);
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

		// check is a concrete class implements the given method name
		private bool IsImplemented(string methodName)
		{
			var mi = this.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			return mi != null && mi.GetBaseDefinition().DeclaringType != mi.DeclaringType;
		}
	}
}
