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
using NLog;
using UnityEditor;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Editor UI for VPX images, equivalent to VPX's "Image Manager" window
	/// </summary>
	public class ImageManager : ManagerWindow<ImageListData>
	{
		protected override string DataTypeName => "Image";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		[MenuItem("Visual Pinball/Image Manager", false, 401)]
		public static void ShowWindow()
		{
			GetWindow<ImageManager>("Image Manager");
		}

		public override void OnEnable()
		{
			titleContent = new GUIContent("Image Manager", EditorGUIUtility.IconContent("RawImage Icon").image);
			base.OnEnable();
		}

		protected override void OnButtonBarGUI() {
			if (GUILayout.Button("Add Textures Referenced in Scene", GUILayout.ExpandWidth(false))) {
				AddReferenced();
			}
		}

		protected override void OnDataDetailGUI()
		{
			SliderField("Alpha Mask", ref _selectedItem.LegacyTexture.AlphaTestValue, 0, 255);

			if (_selectedItem.LegacyTexture.Texture != null) {

				if (GUILayout.Button("Locate")) {
					EditorGUIUtility.PingObject(_selectedItem.LegacyTexture.Texture);
				}

				const float padding = 4f;
				var rect = GUILayoutUtility.GetRect(new GUIContent(""), GUIStyle.none);
				var aspect = (float)_selectedItem.LegacyTexture.Texture.height / _selectedItem.LegacyTexture.Texture.width;
				rect.yMin += padding;
				rect.xMin += padding;
				rect.width = Mathf.Min(_selectedItem.LegacyTexture.Texture.width, rect.width) - padding;
				rect.height = rect.width * aspect;
				GUI.DrawTexture(rect, _selectedItem.LegacyTexture.Texture);

			} else {
				_selectedItem.LegacyTexture.Texture = (Texture)EditorGUILayout.ObjectField(_selectedItem.LegacyTexture.Texture, typeof(Texture), false);
			}
		}

		protected override List<ImageListData> CollectData()
		{
			var data = new List<ImageListData>();

			// collect list of in use textures
			var inUseTextures = new HashSet<string>(GetReferenced().Select(AssetDatabase.GetAssetPath));

			foreach (var t in TableComponent.LegacyContainer.Textures) {
				var inUse = false;
				if (t.Texture != null) {
					inUse = inUseTextures.Contains(AssetDatabase.GetAssetPath(t.Texture));
				}
				data.Add(new ImageListData(t, inUse));
			}

			return data;
		}

		private void AddReferenced()
		{
			var inList = new HashSet<string>(TableComponent.LegacyContainer.Textures.Select(t => AssetDatabase.GetAssetPath(t.Texture)));

			Undo.RecordObject(TableComponent, "Add referenced textures");
			foreach (var refTexture in GetReferenced()) {
				if (!inList.Contains(AssetDatabase.GetAssetPath(refTexture))) {
					TableComponent.LegacyContainer.Textures.Add(new LegacyTexture(refTexture));
				}
			}
			Reload();
		}

		private IEnumerable<Texture> GetReferenced()
		{
			var referenced = new HashSet<Texture>();
			foreach (var mr in TableComponent.GetComponentsInChildren<MeshRenderer>()) {
				if (!mr.sharedMaterial) {
					continue;
				}
				var mainTex = mr.sharedMaterial.mainTexture;
				var normalTex = mr.sharedMaterial.GetTexture(RenderPipeline.Current.MaterialConverter.NormalMapProperty);
				if (mainTex != null && !referenced.Contains(mainTex)) {
					referenced.Add(mainTex);
				}
				if (normalTex != null && !referenced.Contains(normalTex)) {
					referenced.Add(normalTex);
				}
			}
			return referenced;
		}

		protected override void AddNewData(string undoName, string newName) {
			Undo.RecordObject(TableComponent, undoName);
			TableComponent.LegacyContainer.Textures.Add(new LegacyTexture());
		}

		protected override void RemoveData(string undoName, ImageListData data)
		{
			Undo.RecordObject(TableComponent, undoName);
			TableComponent.LegacyContainer.Textures.Remove(data.LegacyTexture);
		}
	}
}
