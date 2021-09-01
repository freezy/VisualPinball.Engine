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

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Editor UI for VPX materials, equivalent to VPX's "Material Manager" window
	/// </summary>
	public class MaterialManager : ManagerWindow<MaterialListData>
	{
		protected override string DataTypeName => "Material";

		[MenuItem("Visual Pinball/Material Manager", false, 403)]
		public static void ShowWindow()
		{
			GetWindow<MaterialManager>("Material Manager");
		}

		public override void OnEnable()
		{
			titleContent = new GUIContent("Material Manager", EditorGUIUtility.IconContent("Material On Icon").image);
			base.OnEnable();
		}

		protected override void OnDataDetailGUI()
		{
			VisualOptions();
		}

		protected override void RenameExistingItem(MaterialListData data, string newName)
		{
			string oldName = data.Material.Name;

			// give each editable item a chance to update its fields
			string undoName = "Rename Material";
			foreach (var item in _tableAuthoring.GetComponentsInChildren<IItemMeshAuthoring>()) {
				RenameReflectedFields(undoName, item, item.MaterialRefs, oldName, newName);
			}
			Undo.RecordObject(_tableAuthoring, undoName);

			data.Material.Name = newName;
		}

		protected override List<MaterialListData> CollectData()
		{
			List<MaterialListData> data = new List<MaterialListData>();

			// collect list of in use materials
			List<string> inUseMaterials = new List<string>();
			foreach (var item in _tableAuthoring.GetComponentsInChildren<IItemMeshAuthoring>()) {
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
			for (int i = 0; i < _tableAuthoring.LegacyContainer.Materials.Count; i++) {
				var mat = _tableAuthoring.LegacyContainer.Materials[i];
				data.Add(new MaterialListData { Material = mat, InUse = inUseMaterials.Contains(mat.Name) });
			}

			return data;
		}

		protected override void OnDataChanged(string undoName, MaterialListData data)
		{
			foreach (var item in _tableAuthoring.GetComponentsInChildren<IItemMeshAuthoring>()) {
				if (IsReferenced(item.MaterialRefs, item.ItemData, data.Material.Name)) {
					Undo.RecordObject(item as Object, undoName);
				}
			}
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
			_tableAuthoring.LegacyContainer.Materials.Add(newMat);
		}

		protected override void RemoveData(string undoName, MaterialListData data)
		{
			_tableAuthoring.LegacyContainer.Materials.Remove(data.Material);
		}

		protected override void CloneData(string undoName, string newName, MaterialListData data)
		{
			var newMat = data.Material.Clone(newName);
			_tableAuthoring.LegacyContainer.Materials.Add(newMat);
		}
	}
}
