using System;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public abstract class LockingTableEditorWindow : BaseEditorWindow
	{
		protected TableComponent TableComponent;
		protected PlayfieldComponent PlayfieldComponent;
		private GUIStyle _lockButtonStyle;
		private bool _windowLocked = false;

		protected abstract void SetTable(TableComponent table);

		public override void OnEnable()
		{
			TableSelector.Instance.OnTableSelected += OnTableSelected;
			base.OnEnable();
		}

		private void OnDestroy()
		{
			TableSelector.Instance.OnTableSelected -= OnTableSelected;
		}

		private void OnTableSelected(object sender, EventArgs e)
		{
			if (!_windowLocked && TableSelector.Instance.SelectedTable != null) {
				TableComponent = TableSelector.Instance.SelectedTable;
				PlayfieldComponent = TableComponent.GetComponentInChildren<PlayfieldComponent>();
				SetTable(TableComponent);
				Repaint();
			}
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
				_lockButtonStyle = "IN LockButton"; // undocumented ui style for the tab bar lock button
			}
			bool wasLocked = _windowLocked;
			_windowLocked = GUI.Toggle(position, _windowLocked, GUIContent.none, _lockButtonStyle);
			if (wasLocked && !_windowLocked) {
				TableComponent = TableSelector.Instance.SelectedTable;
				SetTable(TableComponent);
				Repaint();
			}
		}

		public virtual void AddItemsToMenu(GenericMenu menu)
		{
			menu.AddItem(new GUIContent("Lock"), _windowLocked, () => _windowLocked = !_windowLocked);
		}
	}
}
