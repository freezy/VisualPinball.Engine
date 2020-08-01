using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Editor.Managers
{
	/// <summary>
	/// Editor UI for VPX images, equivalent to VPX's "Image Manager" window
	/// </summary>
	public class ImageManager : ManagerWindow<ImageListData>
	{
		protected override string DataTypeName => "Image";

		[MenuItem("Visual Pinball/Image Manager", false, 102)]
		public static void ShowWindow()
		{
			GetWindow<ImageManager>("Image Manager");
		}

		protected override void OnDataDetailGUI()
		{
			SliderField("Alpha Mask", ref _selectedItem.TextureData.AlphaTestValue, 0, 255);

			var unityTex = _table.GetTexture(_selectedItem.Name);
			if (unityTex != null) {
				var rect = GUILayoutUtility.GetRect(new GUIContent(""), GUIStyle.none);
				float aspect = (float)unityTex.height / unityTex.width;
				rect.width = Mathf.Min(unityTex.width, rect.width);
				rect.height = rect.width * aspect;
				GUI.DrawTexture(rect, unityTex);
			}
		}

		protected override void CollectData(List<ImageListData> data)
		{
			// collect list of in use textures
			List<string> inUseTextures = new List<string>();
			foreach (var item in _table.GetComponentsInChildren<IEditableItemBehavior>()) {
				var texRefs = item.TextureRefs;
				if (texRefs == null) { continue; }
				foreach (var texRef in texRefs) {
					var texName = GetMemberValue(texRef, item.ItemData);
					if (!string.IsNullOrEmpty(texName)) {
						inUseTextures.Add(texName);
					}
				}
			}

			foreach (var t in _table.Table.Textures) {
				var texData = t.Value.Data;
				data.Add(new ImageListData { TextureData = texData, InUse = inUseTextures.Contains(texData.Name)});
			}
		}

		protected override void OnDataChanged(string undoName, ImageListData data)
		{
			// Run over table's texture scriptable object wrappers to find the one being edited and add to the undo stack
			foreach (var tableTex in _table.Textures ) {
				if (tableTex.Data == _selectedItem.TextureData) {
					Undo.RecordObject(tableTex, undoName);
					break;
				}
			}

			// update any items using this tex
			foreach (var item in _table.GetComponentsInChildren<IEditableItemBehavior>()) {
				if (IsReferenced(item.TextureRefs, item.ItemData, data.TextureData.Name)) {
					item.MeshDirty = true;
					Undo.RecordObject(item as Object, undoName);
				}
			}
		}
	}
}
