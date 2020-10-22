using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public abstract class LockingTableEditorWindow : BaseEditorWindow
	{
		protected TableAuthoring _tableAuthoring;
		private GUIStyle _lockButtonStyle;
		private bool _windowLocked = false;

		protected abstract void SetTable(TableAuthoring table);

		protected virtual void OnHierarchyChange()
		{
			// if we don't have a table, look for one when stuff in the scene changes
			if (_tableAuthoring == null) {
				FindTable();
			}
		}

		protected virtual void OnFocus()
		{
			if (_windowLocked) { return; }
			SetTableFromSelection();
		}

		protected virtual void OnSelectionChange()
		{
			if (_windowLocked) { return; }
			SetTableFromSelection();
			Repaint();
		}

		/// <summary>
		/// This is called by unity as part of the GUI pass, its an undocumented feature
		/// that gives us the ability to draw UI in the upper right of the tab bar, so we'll
		/// use it to add the little lock toggle just like inspectors
		/// </summary>
		/// <param name="position"></param>
		protected virtual void ShowButton(Rect position)
		{
			if (_lockButtonStyle == null) {
				_lockButtonStyle = "IN LockButton"; // undocument ui style for the tab bar lock button
			}
			bool wasLocked = _windowLocked;
			_windowLocked = GUI.Toggle(position, _windowLocked, GUIContent.none, _lockButtonStyle);
			if (wasLocked && !_windowLocked) {
				SetTableFromSelection();
			}
		}

		public virtual void AddItemsToMenu(GenericMenu menu)
		{
			menu.AddItem(new GUIContent("Lock"), _windowLocked, () => _windowLocked = !_windowLocked);
		}

		protected void FindTable()
		{
			SetTableFromSelection();
			if (_tableAuthoring == null) {
				// nothing was selected, just use the first found table
				_tableAuthoring = FindObjectOfType<TableAuthoring>();
				SetTable(_tableAuthoring);
			}
		}

		protected void SetTableFromSelection()
		{
			if (Selection.activeGameObject == null) { return; }

			// check to see if the selection's table is different from the current one being used by this manager
			var selectedTable = Selection.activeGameObject.GetComponentInParent<TableAuthoring>();
			if (selectedTable != null) {
				_tableAuthoring = selectedTable;
				SetTable(selectedTable);
			}
		}
	}
}
