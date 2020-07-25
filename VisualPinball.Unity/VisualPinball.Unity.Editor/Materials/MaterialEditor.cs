using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Editor.Materials
{
	/// <summary>
	/// Editor UI for VPX materials, equivalent to VPX's "Material Manager" window
	/// </summary>
	public class MaterialEditor : EditorWindow
	{
		private MaterialListView _listView;
		private TreeViewState _treeViewState;

		private bool _foldoutVisual = true;
		private bool _foldoutPhysics = true;
		private bool _renaming = false;
		private string _renameBuffer = "";

		private TableBehavior _table;
		private Engine.VPT.Material _selectedMaterial;

		[SerializeField] private string _forceSelectMatWithName;

		[MenuItem("Visual Pinball/Material Manager", false, 103)]
		public static void ShowWindow()
		{
			GetWindow<MaterialEditor>("Material Manager");
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
				_selectedMaterial = null;
			}

			if (!string.IsNullOrEmpty(_forceSelectMatWithName)) {
				_listView.SelectMaterialWithName(_forceSelectMatWithName);
				_forceSelectMatWithName = null;
			}

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Add", GUILayout.ExpandWidth(false))) {
				AddNewMaterial();
			}
			if (GUILayout.Button("Remove", GUILayout.ExpandWidth(false))) {
				RemoveMaterial(_selectedMaterial);
			}
			if (GUILayout.Button("Clone", GUILayout.ExpandWidth(false))) {
				CloneMaterial(_selectedMaterial);
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
			if (_selectedMaterial != null) {
				EditorGUILayout.BeginHorizontal();
				if (_renaming) {
					_renameBuffer = EditorGUILayout.TextField(_renameBuffer);
					if (GUILayout.Button("Save")) {
						RenameExistingMaterial(_selectedMaterial, _renameBuffer);
						_renaming = false;
						_listView.Reload();
					}
					if (GUILayout.Button("Cancel")) {
						_renaming = false;
						GUI.FocusControl(""); // de-focus on cancel because unity will retain previous buffer text until focus changes
					}
				} else {
					EditorGUILayout.LabelField(_selectedMaterial.Name);
					if (GUILayout.Button("Rename")) {
						_renaming = true;
						_renameBuffer = _selectedMaterial.Name;
					}
				}
				EditorGUILayout.EndHorizontal();

				if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
					EditorGUI.indentLevel++;
					PhysicsOptions();
					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndFoldoutHeaderGroup();

				if (_foldoutVisual = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutVisual, "Visual")) {
					EditorGUI.indentLevel++;
					VisualOptions();
					EditorGUI.indentLevel--;
				}
				EditorGUILayout.EndFoldoutHeaderGroup();
			} else {
				EditorGUILayout.LabelField("Select material to edit");
			}
			EditorGUILayout.EndVertical();

			EditorGUILayout.EndHorizontal();
		}

		private void PhysicsOptions()
		{
			FloatField("Elasticity", ref _selectedMaterial.Elasticity);
			FloatField("Elasticity Falloff", ref _selectedMaterial.ElasticityFalloff);
			FloatField("Friction", ref _selectedMaterial.Friction);
			FloatField("Scatter Angle", ref _selectedMaterial.ScatterAngle);
		}

		private void VisualOptions()
		{
			ToggleField("Metal Material", ref _selectedMaterial.IsMetal, "disables Glossy Layer and has stronger reflectivity");
			EditorGUILayout.Space();

			ColorField("Base Color", ref _selectedMaterial.BaseColor, "Steers the basic Color of an Object. Wrap allows for light even if object is only lit from behind (0=natural)");
			SliderField("Wrap", ref _selectedMaterial.WrapLighting);
			EditorGUILayout.Space();

			ColorField("Glossy Layer", ref _selectedMaterial.Glossiness, "Add subtle reflections on non-metal materials (leave at non-black for most natural behavior)");
			SliderField("Use Image", ref _selectedMaterial.GlossyImageLerp);
			EditorGUILayout.Space();

			SliderField("Shininess", ref _selectedMaterial.Roughness, tooltip: "Sets from very dull (Shininess low) to perfect/mirror-like (Shininess high) reflections (for glossy layer or metal only)");
			EditorGUILayout.Space();

			ColorField("Clearcoat Layer", ref _selectedMaterial.ClearCoat, "Add an additional thin clearcoat layer on top of the material");
			EditorGUILayout.Space();

			SliderField("Edge Brightness", ref _selectedMaterial.Edge, tooltip: "Dims the silhouette \"glow\" when using Glossy or Clearcoat Layers (1=natural, 0=dark)");
			EditorGUILayout.Space();

			EditorGUILayout.LabelField(new GUIContent("Opacity", "will be modulated by Image/Alpha channel on Object"));
			EditorGUI.indentLevel++;
			ToggleField("Active", ref _selectedMaterial.IsOpacityActive);
			SliderField("Amount", ref _selectedMaterial.Opacity);
			SliderField("Edge Opacity", ref _selectedMaterial.EdgeAlpha, tooltip: "Increases the opacity on the silhouette (1=natural, 0=no change)");
			SliderField("Thickness", ref _selectedMaterial.Thickness, tooltip: "Interacts with Edge Opacity & Amount, can provide a more natural result for thick materials (1=thick, 0=no change)");
			EditorGUI.indentLevel--;
		}

		private void FloatField(string label, ref float field)
		{
			EditorGUI.BeginChangeCheck();
			float val = EditorGUILayout.FloatField(label, field);
			if (EditorGUI.EndChangeCheck()) {
				FinalizeChange(label, ref field, val);
			}
		}

		private void SliderField(string label, ref float field, float min = 0f, float max = 1f, string tooltip = "")
		{
			EditorGUI.BeginChangeCheck();
			float val = EditorGUILayout.Slider(new GUIContent(label, tooltip), field, min, max);
			if (EditorGUI.EndChangeCheck()) {
				FinalizeChange(label, ref field, val);
			}
		}

		private void ToggleField(string label, ref bool field, string tooltip = "")
		{
			EditorGUI.BeginChangeCheck();
			bool val = EditorGUILayout.Toggle(new GUIContent(label, tooltip), field);
			if (EditorGUI.EndChangeCheck()) {
				FinalizeChange(label, ref field, val);
			}
		}

		private void ColorField(string label, ref Engine.Math.Color field, string tooltip = "")
		{
			EditorGUI.BeginChangeCheck();
			Engine.Math.Color val = EditorGUILayout.ColorField(new GUIContent(label, tooltip), field.ToUnityColor()).ToEngineColor();
			if (EditorGUI.EndChangeCheck()) {
				FinalizeChange(label, ref field, val);
			}
		}

		private void FinalizeChange<T>(string label, ref T field, T val)
		{
			string undoName = "Edit Material: " + label;
			DirtyMeshesWithMaterial(undoName, _selectedMaterial);
			Undo.RecordObject(_table, undoName);
			field = val;
			SceneView.RepaintAll();
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
			_listView = new MaterialListView(_treeViewState, _table, MaterialSelected);
		}

		private void MaterialSelected(List<Engine.VPT.Material> selectedMaterials)
		{
			_selectedMaterial = null;
			if (selectedMaterials.Count > 0) {
				_selectedMaterial = selectedMaterials[0]; // not supporting multi select for now
				_renaming = false;
			}
			Repaint();
		}

		private void DirtyMeshesWithMaterial(string undoName, Engine.VPT.Material material)
		{
			string matName = material.Name.ToLower();
			foreach (var item in _table.GetComponentsInChildren<IEditableItemBehavior>()) {
				if (item.UsedMaterials != null && item.UsedMaterials.Any( um => um.ToLower() == matName )) {
					item.MeshDirty = true;
					Undo.RecordObject(item as Object, undoName);
				}
			}
		}

		// sets the name property of the material, checking for name collions and appending a number to avoid it
		private void RenameExistingMaterial(Engine.VPT.Material material, string desiredName)
		{
			if (string.IsNullOrEmpty(desiredName)) { // don't allow empty names
				return;
			}

			string oldName = material.Name;
			string acceptedName = GetUniqueMaterialName(desiredName, material);

			// give each editable item a chance to update its fields
			string undoName = "Rename Material";
			foreach (var item in _table.GetComponentsInChildren<IEditableItemBehavior>()) {
				item.HandleMaterialRenamed(undoName, oldName, acceptedName);
			}
			Undo.RecordObject(_table, undoName);

			material.Name = acceptedName;
		}

		private string GetUniqueMaterialName(string desiredName, Engine.VPT.Material ignore = null)
		{
			string acceptedName = desiredName;
			int appendNum = 1;
			while (IsNameInUse(acceptedName, ignore)) {
				acceptedName = desiredName + appendNum;
				appendNum++;
			}
			return acceptedName;
		}

		private bool IsNameInUse(string name, Engine.VPT.Material ignore = null)
		{
			foreach (var mat in _table.Item.Data.Materials) {
				if (mat != ignore && name == mat.Name) {
					return true;
				}
			}
			return false;
		}

		private void AddNewMaterial()
		{
			var newMat = new Engine.VPT.Material(GetUniqueMaterialName("New Material"));
			AddMaterialToTable("Clone Material", newMat);
		}

		private void RemoveMaterial(Engine.VPT.Material material)
		{
			if (!EditorUtility.DisplayDialog("Delete Material", $"Are you sure want to delete \"{material.Name}\"?", "Delete", "Cancel")) {
				return;
			}

			_selectedMaterial = null;
			Undo.RecordObjects(new Object[] { this, _table }, "Remove Material");

			_table.data.Materials = _table.data.Materials.Where(m => m != material).ToArray();
			_table.data.NumMaterials = _table.data.Materials.Length;

			_listView.Reload();
		}

		private void CloneMaterial(Engine.VPT.Material material)
		{
			var newMat = material.Clone(GetUniqueMaterialName(material.Name));
			AddMaterialToTable("Clone Material", newMat);
		}

		private void AddMaterialToTable(string undoName, Engine.VPT.Material material)
		{
			// use a serialized field to force material selection in the next gui pass
			// this way undo will cause it to happen again, and if its no there anymore, just deselect any
			_forceSelectMatWithName = material.Name;
			Undo.RecordObjects(new Object[] { this, _table }, undoName);

			_table.data.Materials = _table.data.Materials.Append(material).ToArray();
			_table.data.NumMaterials = _table.data.Materials.Length;

			_listView.Reload();
		}
	}
}
