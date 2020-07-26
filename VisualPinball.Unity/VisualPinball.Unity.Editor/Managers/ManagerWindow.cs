using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Editor.Managers
{
	/// <summary>
	/// Base class for VPX-style "Manager" windows, such as the Material Manager
	/// </summary>
	/// <typeparam name="T">class of type IManagerListData that represents the data being edited</typeparam>
	public abstract class ManagerWindow<T> : EditorWindow where T: class, IManagerListData
	{
		protected TableBehavior _table;
		protected T _selectedItem;

		private List<T> _data = new List<T>();
		private ManagerListView<T> _listView;
		private TreeViewState _treeViewState;
		private bool _renaming = false;
		private string _renameBuffer = "";
		[SerializeField] private string _forceSelectItemWithName;

		protected abstract void OnItemDetailGUI();
		protected abstract void RenameExistingItem(T item, string desiredName);
		protected abstract void CollectData(List<T> data);

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
			if (GUILayout.Button("Add", GUILayout.ExpandWidth(false))) {
				//AddNewMaterial();
			}
			if (GUILayout.Button("Remove", GUILayout.ExpandWidth(false))) {
				//RemoveMaterial(_selectedMaterial);
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();

			// list
			GUILayout.FlexibleSpace();
			var r = GUILayoutUtility.GetLastRect();
			var listRect = new Rect(r.x, r.y, r.width, position.height - r.y);
			_listView.OnGUI(listRect);

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
						_listView.Reload();
					}
					if (GUILayout.Button("Cancel")) {
						_renaming = false;
						GUI.FocusControl(""); // de-focus on cancel because unity will retain previous buffer text until focus changes
					}
				} else {
					EditorGUILayout.LabelField(_selectedItem.Name);
					if (GUILayout.Button("Rename")) {
						_renaming = true;
						_renameBuffer = _selectedItem.Name;
					}
				}
				EditorGUILayout.EndHorizontal();

				OnItemDetailGUI();
			} else {
				EditorGUILayout.LabelField("Nothing selected");
			}
			EditorGUILayout.EndVertical();

			EditorGUILayout.EndHorizontal();
		}

		private void UndoPerformed()
		{
			if (_listView != null) {
				_listView.Reload();
			}
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
			CollectData(_data);
			_listView = new ManagerListView<T>(_treeViewState, _data, ItemSelected);

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

		private bool IsNameInUse(string name, T ignore = null)
		{
			foreach (var item in _data) {
				if (item != ignore && name.ToLower() == item.Name.ToLower()) {
					return true;
				}
			}
			return false;
		}
	}
}
