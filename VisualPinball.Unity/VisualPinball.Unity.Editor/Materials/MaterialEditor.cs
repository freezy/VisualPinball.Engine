using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Editor.Materials
{
	/// <summary>
	/// Editor UI for VPX materials, equivalent to VPX's "Material Manager" window
	/// </summary>
	public class MaterialEditor : EditorWindow
	{
		private TreeViewTest _treeView;
		private TreeViewState _treeViewState;

		private bool _foldoutVisual = true;
		private bool _foldoutPhysics = true;
		private bool _renaming = false;
		private string _renameBuffer = "";

		private TableBehavior _table;
		private Engine.VPT.Material _selectedMaterial;

		[MenuItem("Visual Pinball/Material Manager", false, 102)]
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

			_table = GameObject.FindObjectOfType<TableBehavior>();
			_treeView = new TreeViewTest(_treeViewState, _table, MaterialSelected);
		}

		protected virtual void OnGUI()
		{
			EditorGUILayout.BeginHorizontal();

			// list
			GUILayout.FlexibleSpace();
			var r = GUILayoutUtility.GetLastRect();
			_treeView.OnGUI(new Rect(0, 0, r.width, position.height));

			// options
			EditorGUILayout.BeginVertical(GUILayout.MaxWidth(300));
			if (_selectedMaterial != null) {
				EditorGUILayout.BeginHorizontal();
				if (_renaming) {
					_renameBuffer = EditorGUILayout.TextField(_renameBuffer);
					if (GUILayout.Button("Save")) {
						Undo.RecordObject(_table, "Rename Material");
						_selectedMaterial.Name = _renameBuffer;
						_renaming = false;
						_treeView.Reload();
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
				Undo.RecordObject(_table, "Edit Material: " + label);
				field = val;
			}
		}

		private void SliderField(string label, ref float field, float min = 0f, float max = 1f, string tooltip = "")
		{
			EditorGUI.BeginChangeCheck();
			float val = EditorGUILayout.Slider(new GUIContent(label, tooltip), field, min, max);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(_table, "Edit Material: " + label);
				field = val;
			}
		}

		private void ToggleField(string label, ref bool field, string tooltip = "")
		{
			EditorGUI.BeginChangeCheck();
			bool val = EditorGUILayout.Toggle(new GUIContent(label, tooltip), field);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(_table, "Edit Material: " + label);
				field = val;
			}
		}

		private void ColorField(string label, ref Engine.Math.Color field, string tooltip = "")
		{
			EditorGUI.BeginChangeCheck();
			Engine.Math.Color val = EditorGUILayout.ColorField(new GUIContent(label, tooltip), field.ToUnityColor()).ToEngineColor();
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(_table, "Edit Material: " + label);
				field = val;
			}
		}

		private void UndoPerformed()
		{
			if (_treeView != null) {
				_treeView.Reload();
			}
		}

		private void MaterialSelected(List<Engine.VPT.Material> selectedMaterials)
		{
			_selectedMaterial = null;
			if (selectedMaterials.Count > 0) {
				_selectedMaterial = selectedMaterials[0]; // TODO: multi select stuff?
				_renaming = false;
			}
			Repaint();
		}
	}

	class TreeViewTest : TreeView // TODO: rename and move to its own file
	{
		public event Action<List<Engine.VPT.Material>> MaterialSelected;

		private TableBehavior _table;
		private List<Engine.VPT.Material> _materials = new List<Engine.VPT.Material>();

		public TreeViewTest(TreeViewState treeViewState, TableBehavior table, Action<List<Engine.VPT.Material>> materialSelected) : base(treeViewState)
		{
			MaterialSelected += materialSelected;
			_table = table;

			var columns = new[]
			{
				new MultiColumnHeaderState.Column
				{
					headerContent = new GUIContent("Name"),
					headerTextAlignment = TextAlignment.Left,
					canSort = true,
					sortedAscending = true,
					sortingArrowAlignment = TextAlignment.Right,
					width = 300,
					minWidth = 100,
					maxWidth = float.MaxValue,
					autoResize = true,
					allowToggleVisibility = false,
				},
				new MultiColumnHeaderState.Column
				{
					headerContent = new GUIContent("In use"),
					headerTextAlignment = TextAlignment.Left,
					canSort = true,
					sortedAscending = true,
					sortingArrowAlignment = TextAlignment.Right,
					width = 50,
					minWidth = 50,
					maxWidth = 50,
					autoResize = true,
					allowToggleVisibility = false,
				},
			};

			var headerState = new MultiColumnHeaderState(columns);
			this.multiColumnHeader = new MultiColumnHeader(headerState);
			this.multiColumnHeader.SetSorting(0, true);
			this.multiColumnHeader.sortingChanged += SortingChanged;
			this.showAlternatingRowBackgrounds = true;
			this.showBorder = true;

			Reload();
			if (_materials.Count > 0) {
				SetSelection(new List<int> { 0 }, TreeViewSelectionOptions.FireSelectionChanged);
			}
		}

		private void SortingChanged(MultiColumnHeader multiColumnHeader)
		{
			Reload();
		}

		protected override TreeViewItem BuildRoot()
		{
			return new TreeViewItem { id = -1, depth = -1, displayName = "Root" };
		}

		protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
		{
			var items = new List<TreeViewItem>();
			if (_table == null) return items;

			_materials.Clear();
			_materials.AddRange(_table.Item.Data.Materials);
			for (int i = 0; i < _materials.Count; i++) {
				var mat = _materials[i];
				items.Add(new TreeViewItem { id = i, depth = 0, displayName = mat.Name });
			}

			var sortedColumns = this.multiColumnHeader.state.sortedColumns;
			if (sortedColumns.Length > 0) {
				bool ascending = multiColumnHeader.IsSortedAscending(sortedColumns[0]);
				switch (sortedColumns[0]) {
					case 0:
						if (ascending) {
							items.Sort((a, b) => {
								return a.displayName.CompareTo(b.displayName);
							});
						} else {
							items.Sort((a, b) => {
								return b.displayName.CompareTo(a.displayName);
							});
						}
						break;
				}
			}

			return items;
		}

		protected override void RowGUI(RowGUIArgs args)
		{
			for (int i = 0; i < args.GetNumVisibleColumns(); ++i) {
				CellGUI(args.GetCellRect(i), args.item, args.GetColumn(i));
			}
		}

		private void CellGUI(Rect cellRect, TreeViewItem item, int column)
		{
			CenterRectUsingSingleLineHeight(ref cellRect);
			switch (column) {
				case 0: // todo: make an enum for the columns
					GUI.Label(cellRect, item.displayName);
					break;
			}
		}

		protected override void SelectionChanged(IList<int> selectedIds)
		{
			List<Engine.VPT.Material> selectedMats = new List<Engine.VPT.Material>();
			foreach (var id in selectedIds) {
				if (id >= 0 && id < _materials.Count) {
					selectedMats.Add(_materials[id]);
				}
			}
			MaterialSelected?.Invoke(selectedMats);
		}
	}
}
