using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Extensions;
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

		protected override void OnButtonBarGUI() {
			if (GUILayout.Button("Update All", GUILayout.ExpandWidth(false))) {
				UpdateAllImages();
			}
		}

		protected override void OnDataDetailGUI()
		{
			SliderField("Alpha Mask", ref _selectedItem.TextureData.AlphaTestValue, 0, 255);

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PrefixLabel("Replace Image");
			var tex = (UnityEngine.Texture2D)EditorGUILayout.ObjectField(null, typeof(UnityEngine.Texture2D), false);
			EditorGUILayout.EndHorizontal();
			if (EditorGUI.EndChangeCheck() && tex != null) {
				ReplaceImageFromAsset(_selectedItem.TextureData, tex);
			}
			if (GUILayout.Button("Export Image")) {
				ExportImage();
			}
			if (GUILayout.Button("Export Image as PNG")) {
				ExportImageAsPng();
			}

			var unityTex = _table.GetTexture(_selectedItem.Name);
			if (unityTex != null) {
				var rect = GUILayoutUtility.GetRect(new GUIContent(""), GUIStyle.none);
				float aspect = (float)unityTex.height / unityTex.width;
				rect.width = Mathf.Min(unityTex.width, rect.width);
				rect.height = rect.width * aspect;
				GUI.DrawTexture(rect, unityTex);
			}
		}

		protected override void RenameExistingItem(ImageListData data, string newName)
		{
			_table.Textures.SetNameMapDirty();
			string oldName = data.TextureData.Name;

			// give each editable item a chance to update its fields
			string undoName = "Rename Material";
			foreach (var item in _table.GetComponentsInChildren<IEditableItemBehavior>()) {
				RenameReflectedFields(undoName, item, item.TextureRefs, oldName, newName);
			}
			RecordUndo(undoName, data.TextureData);

			data.TextureData.Name = newName;
		}

		protected override List<ImageListData> CollectData()
		{
			List<ImageListData> data = new List<ImageListData>();

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

			foreach (var t in _table.Textures) {
				var texData = t.Data;
				data.Add(new ImageListData { TextureData = texData, InUse = inUseTextures.Contains(texData.Name)});
			}

			return data;
		}

		protected override void OnDataChanged(string undoName, ImageListData data)
		{
			OnDataChanged(undoName, data.TextureData);
		}

		protected override void AddNewData(string undoName, string newName) {
			_table.Textures.SetNameMapDirty();
			var sidecar = _table.GetComponentInChildren<TableSidecar>();
			if (sidecar != null) {
				Undo.RecordObject(sidecar, undoName);
			}

			var newTex = new Engine.VPT.Texture(newName);
			_table.Textures.Add(newTex);
			_table.Item.Data.NumTextures = _table.Textures.Count;
		}

		protected override void RemoveData(string undoName, ImageListData data)
		{
			_table.Textures.SetNameMapDirty();
			var sidecar = _table.GetComponentInChildren<TableSidecar>();
			if (sidecar != null) {
				Undo.RecordObject(sidecar, undoName);
			}

			_table.Textures.Remove(data.Name);
			_table.Item.Data.NumTextures = _table.Textures.Count;
		}

		private void OnDataChanged(string undoName, TextureData textureData)
		{
			RecordUndo(undoName, textureData);

			// update any items using this tex
			foreach (var item in _table.GetComponentsInChildren<IEditableItemBehavior>()) {
				if (IsReferenced(item.TextureRefs, item.ItemData, textureData.Name)) {
					item.MeshDirty = true;
					Undo.RecordObject(item as UnityEngine.Object, undoName);
				}
			}
		}

		private void RecordUndo(string undoName, TextureData textureData)
		{
			if (_table == null) { return; }

			// Run over table's texture scriptable object wrappers to find the one being edited and add to the undo stack
			foreach (var tableTex in _table.Textures.SerializedObjects) {
				if (tableTex.Data == textureData) {
					Undo.RecordObject(tableTex, undoName);
					break;
				}
			}
		}

		private void UpdateAllImages()
		{
			foreach (var t in _table.Textures) {
				ReplaceImageFromPath(t.Data, t.Data.Path);
			}
		}

		private void ReplaceImageFromPath(TextureData textureData, string path) {
			if (_table == null || textureData == null || string.IsNullOrEmpty(path)) { return; }

			byte[] newBytes = null;
			try {
				newBytes = File.ReadAllBytes(path);
			} catch (Exception ex) {
				Debug.LogException(ex);
			}
			if (newBytes == null) { return; }

			string undoName = "Replace Image";

			_table.MarkTextureDirty(textureData.Name);
			Undo.RecordObject(_table, undoName);
			OnDataChanged(undoName, textureData);

			textureData.Binary.Data = newBytes;
			textureData.Binary.Size = newBytes.Length;
			textureData.Path = path;

			// update size values assuming we loaded alright
			var unityTex = _table.GetTexture(textureData.Name);
			if (unityTex != null) {
				textureData.Width = unityTex.width;
				textureData.Height = unityTex.height;
			}
		}

		private void ReplaceImageFromAsset(TextureData textureData, UnityEngine.Texture2D tex)
		{
			string path = AssetDatabase.GetAssetPath(tex);
			if (!string.IsNullOrEmpty(path)) {
				ReplaceImageFromPath(textureData, path);
			}
		}

		private void ExportImage()
		{
			if (_table == null || _selectedItem == null) { return; }

			var unityTex = _table.GetTexture(_selectedItem.TextureData.Name);
			if (unityTex != null) {
				string fileExt = Path.GetExtension(_selectedItem.TextureData.Path).TrimStart('.');
				if (string.IsNullOrEmpty(fileExt)) {
					Debug.LogError("Could not determine filetype from path");
				}
				string savePath = EditorUtility.SaveFilePanelInProject("Export Image", unityTex.name, fileExt, "Export Image");
				if (!string.IsNullOrEmpty(savePath)) {
					File.WriteAllBytes(savePath, _selectedItem.TextureData.Binary.Data);
					AssetDatabase.ImportAsset(savePath);
				}
			}
		}

		private void ExportImageAsPng()
		{
			if (_table == null || _selectedItem == null) { return; }

			var unityTex = _table.GetTexture(_selectedItem.TextureData.Name);
			if (unityTex != null) {
				string savePath = EditorUtility.SaveFilePanelInProject("Export Image", unityTex.name, "png", "Export Image");
				if (!string.IsNullOrEmpty(savePath)) {
					File.WriteAllBytes(savePath, unityTex.EncodeToPNG());
					AssetDatabase.ImportAsset(savePath);
				}
			}
		}
	}
}
