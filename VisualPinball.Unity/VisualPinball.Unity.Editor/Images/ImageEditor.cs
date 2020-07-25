using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Editor.Images
{
	/// <summary>
	/// Editor UI for VPX images, equivalent to VPX's "Image Manager" window
	/// </summary>
	public class ImageEditor : EditorWindow
	{
		private ImageListView _listView;
		private TreeViewState _treeViewState;

		private bool _renaming = false;
		private string _renameBuffer = "";

		private TableBehavior _table;
		private Engine.VPT.Texture _selectedImage;

		[SerializeField] private string _forceSelectTexWithName;

		[MenuItem("Visual Pinball/Image Manager", false, 102)]
		public static void ShowWindow()
		{
			GetWindow<ImageEditor>("Image Manager");
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
			// if the table went away, clear the selected material as well
			if (_table == null) {
				_selectedImage = null;
			}

			if (!string.IsNullOrEmpty(_forceSelectTexWithName)) {
				_listView.SelectTextureWithName(_forceSelectTexWithName);
				_forceSelectTexWithName = null;
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
			if (_selectedImage != null) {
				EditorGUILayout.BeginHorizontal();
				if (_renaming) {
					_renameBuffer = EditorGUILayout.TextField(_renameBuffer);
					if (GUILayout.Button("Save")) {
						RenameExistingImage(_selectedImage, _renameBuffer);
						_renaming = false;
						_listView.Reload();
					}
					if (GUILayout.Button("Cancel")) {
						_renaming = false;
						GUI.FocusControl(""); // de-focus on cancel because unity will retain previous buffer text until focus changes
					}
				} else {
					EditorGUILayout.LabelField(_selectedImage.Name);
					if (GUILayout.Button("Rename")) {
						_renaming = true;
						_renameBuffer = _selectedImage.Name;
					}
				}
				EditorGUILayout.EndHorizontal();


				
				EditorGUILayout.EndFoldoutHeaderGroup();
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

		private void FindTable()
		{
			_table = GameObject.FindObjectOfType<TableBehavior>();
			_listView = new ImageListView(_treeViewState, _table, ImageSelected);
		}

		private void ImageSelected(List<Engine.VPT.Texture> selectedImages)
		{
			_selectedImage = null;
			if (selectedImages.Count > 0) {
				_selectedImage = selectedImages[0]; // not supporting multi select for now
				_renaming = false;
			}
			Repaint();
		}

		// sets the name property of the material, checking for name collions and appending a number to avoid it
		private void RenameExistingImage(Engine.VPT.Texture tex, string desiredName)
		{
			if (string.IsNullOrEmpty(desiredName)) { // don't allow empty names
				return;
			}

			string oldName = tex.Name;
			string acceptedName = GetUniqueTextureName(desiredName, tex);

			// give each editable item a chance to update its fields
			string undoName = "Rename Material";
			foreach (var item in _table.GetComponentsInChildren<IEditableItemBehavior>()) {
				item.HandleTextureRenamed(undoName, oldName, acceptedName);
			}
			Undo.RecordObject(_table, undoName);

			tex.Name = acceptedName;
		}

		private string GetUniqueTextureName(string desiredName, Engine.VPT.Texture ignore = null)
		{
			string acceptedName = desiredName;
			int appendNum = 1;
			while (IsNameInUse(acceptedName, ignore)) {
				acceptedName = desiredName + appendNum;
				appendNum++;
			}
			return acceptedName;
		}

		// TODO: this and mat should compare tolower
		private bool IsNameInUse(string name, Engine.VPT.Texture ignore = null)
		{
			foreach (var tex in _table.Item.Textures.Values) {
				if (tex != ignore && name == tex.Name) {
					return true;
				}
			}
			return false;
		}

	}
}
