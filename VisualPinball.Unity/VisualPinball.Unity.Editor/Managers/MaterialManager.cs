using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Unity.VPT;

namespace VisualPinball.Unity.Editor.Managers
{
	/// <summary>
	/// Editor UI for VPX materials, equivalent to VPX's "Material Manager" window
	/// </summary>
	public class MaterialManager : ManagerWindow<MaterialListData>
	{
		protected override string DataTypeName => "Material";

		private bool _foldoutVisual = true;
		private bool _foldoutPhysics = true;

		[MenuItem("Visual Pinball/Material Manager", false, 103)]
		public static void ShowWindow()
		{
			GetWindow<MaterialManager>("Material Manager");
		}

		protected override void OnDataDetailGUI()
		{
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
		}

		protected override void RenameExistingItem(MaterialListData data, string newName)
		{
			string oldName = data.Material.Name;

			// give each editable item a chance to update its fields
			string undoName = "Rename Material";
			foreach (var item in _table.GetComponentsInChildren<IEditableItemBehavior>()) {
				RenameReflectedFields(undoName, item, item.MaterialRefs, oldName, newName);
			}
			Undo.RecordObject(_table, undoName);

			data.Material.Name = newName;
		}

		protected override void CollectData(List<MaterialListData> data)
		{
			// collect list of in use materials
			List<string> inUseMaterials = new List<string>();
			foreach (var item in _table.GetComponentsInChildren<IEditableItemBehavior>()) {
				var matRefs = item.MaterialRefs;
				if (matRefs == null) { continue; }
				foreach (var matRef in matRefs) {
					var matName = GetMemberValue(matRef, item.ItemData);
					if (!string.IsNullOrEmpty(matName)) {
						inUseMaterials.Add(matName);
					}
				}
			}

			// get row data for each material
			for (int i = 0; i < _table.Item.Data.Materials.Length; i++) {
				var mat = _table.Item.Data.Materials[i];
				data.Add(new MaterialListData { Material = mat, InUse = inUseMaterials.Contains(mat.Name) });
			}
		}

		protected override void OnDataChanged(string undoName, MaterialListData data)
		{
			foreach (var item in _table.GetComponentsInChildren<IEditableItemBehavior>()) {
				if (IsReferenced(item.MaterialRefs, item.ItemData, data.Material.Name)) {
					item.MeshDirty = true;
					Undo.RecordObject(item as Object, undoName);
				}
			}
		}

		private void PhysicsOptions()
		{
			var mat = _selectedItem?.Material;
			if (mat == null) { return; }

			FloatField("Elasticity", ref mat.Elasticity);
			FloatField("Elasticity Falloff", ref mat.ElasticityFalloff);
			FloatField("Friction", ref mat.Friction);
			FloatField("Scatter Angle", ref mat.ScatterAngle);
		}

		private void VisualOptions()
		{
			var mat = _selectedItem?.Material;
			if (mat == null) { return; }

			ToggleField("Metal Material", ref mat.IsMetal, "disables Glossy Layer and has stronger reflectivity");
			EditorGUILayout.Space();

			ColorField("Base Color", ref mat.BaseColor, "Steers the basic Color of an Object. Wrap allows for light even if object is only lit from behind (0=natural)");
			SliderField("Wrap", ref mat.WrapLighting);
			EditorGUILayout.Space();

			ColorField("Glossy Layer", ref mat.Glossiness, "Add subtle reflections on non-metal materials (leave at non-black for most natural behavior)");
			SliderField("Use Image", ref mat.GlossyImageLerp);
			EditorGUILayout.Space();

			SliderField("Shininess", ref mat.Roughness, tooltip: "Sets from very dull (Shininess low) to perfect/mirror-like (Shininess high) reflections (for glossy layer or metal only)");
			EditorGUILayout.Space();

			ColorField("Clearcoat Layer", ref mat.ClearCoat, "Add an additional thin clearcoat layer on top of the material");
			EditorGUILayout.Space();

			SliderField("Edge Brightness", ref mat.Edge, tooltip: "Dims the silhouette \"glow\" when using Glossy or Clearcoat Layers (1=natural, 0=dark)");
			EditorGUILayout.Space();

			EditorGUILayout.LabelField(new GUIContent("Opacity", "will be modulated by Image/Alpha channel on Object"));
			EditorGUI.indentLevel++;
			ToggleField("Active", ref mat.IsOpacityActive);
			SliderField("Amount", ref mat.Opacity);
			SliderField("Edge Opacity", ref mat.EdgeAlpha, tooltip: "Increases the opacity on the silhouette (1=natural, 0=no change)");
			SliderField("Thickness", ref mat.Thickness, tooltip: "Interacts with Edge Opacity & Amount, can provide a more natural result for thick materials (1=thick, 0=no change)");
			EditorGUI.indentLevel--;
		}

		protected override void AddNewData(string undoName, string newName)
		{
			var newMat = new Engine.VPT.Material(newName);
			_table.data.Materials = _table.data.Materials.Append(newMat).ToArray();
			_table.data.NumMaterials = _table.data.Materials.Length;
		}

		protected override void RemoveData(string undoName, MaterialListData data) {
			_table.data.Materials = _table.data.Materials.Where(m => m != data.Material).ToArray();
			_table.data.NumMaterials = _table.data.Materials.Length;
		}

		protected override void CloneData(string undoName, string newName, MaterialListData data)
		{
			var newMat = data.Material.Clone(newName);
			_table.data.Materials = _table.data.Materials.Append(newMat).ToArray();
			_table.data.NumMaterials = _table.data.Materials.Length;
		}
	}
}
