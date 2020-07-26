using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.VPT;

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
			var unityTex = _table.GetTexture(_selectedItem.Name);
			var rect = GUILayoutUtility.GetRect(new GUIContent(""), GUIStyle.none);
			float aspect = (float)unityTex.height / unityTex.width;
			rect.width = Mathf.Min(unityTex.width, rect.width);
			rect.height = rect.width * aspect;
			GUI.DrawTexture(rect, unityTex);
		}

		protected override void RenameExistingItem(ImageListData listItem, string newName)
		{
			string oldName = listItem.Texture.Name;

			// give each editable item a chance to update its fields
			string undoName = "Rename Material";
			foreach (var item in _table.GetComponentsInChildren<IEditableItemBehavior>()) {
				item.HandleTextureRenamed(undoName, oldName, newName);
			}
			Undo.RecordObject(_table, undoName);

			listItem.Texture.Name = newName;
		}

		protected override void CollectData(List<ImageListData> data)
		{
			foreach (var t in _table.Item.Textures) {
				data.Add(new ImageListData { Texture = t.Value });
			}
		}

		protected override void OnDataChanged(string undoName, ImageListData data)
		{

		}
	}
}
