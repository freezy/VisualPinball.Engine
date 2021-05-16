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
using System.IO;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Editor
{
	public class VpxSceneConverter
	{
		private readonly Table _table;
		private GameObject _tableGo;
		private TableAuthoring _tableAuthoring;

		private GameObject _playfieldGo;

		private string _assetsTextures;
		private string _assetsMaterials;

		private readonly Dictionary<string, GameObject> _groupParents = new Dictionary<string, GameObject>();
		private readonly Dictionary<string, string> _texturePaths = new Dictionary<string, string>();

		private static readonly Quaternion GlobalRotation = Quaternion.Euler(-90, 0, 0);
		public const float GlobalScale = 0.001f;

		public VpxSceneConverter(Table table)
		{
			_table = table;
		}

		public GameObject Convert(string filename)
		{
			CreateRootHierarchy();

			try {
				// pause asset database refreshing
				AssetDatabase.StartAssetEditing();
				CreateFileHierarchy();

				ExtractTextures();

				ConvertGameItems();

			} finally {

				// resume asset database refreshing
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
			}

			return _tableGo;
		}

		private void ConvertGameItems()
		{
			var item = _table as IItem;

			var parentGo = GetGroupParent(_table);

			var itemGo = new GameObject(item.Name);
			itemGo.transform.SetParent(parentGo.transform, false);

			_table.SetupGameObject(itemGo);

			// apply transformation
			if (item is IRenderable renderable) {
				itemGo.transform.SetFromMatrix(renderable.TransformationMatrix(_table, Origin.Original).ToUnityMatrix());
			}
		}

		private void ExtractTextures()
		{
			foreach (var texture in _table.Textures) {
				var path = texture.GetUnityFilename(_assetsTextures);
				File.WriteAllBytes(path, texture.Content);
				_texturePaths[texture.Name] = path;
			}
		}

		private void CreateFileHierarchy()
		{
			if (!Directory.Exists("Assets/Tables/")) {
				Directory.CreateDirectory("Assets/Tables/");
			}

			var assetsTableRoot = $"Assets/Tables/{_table.Name}/";
			if (!Directory.Exists(assetsTableRoot)) {
				Directory.CreateDirectory(assetsTableRoot);
			}

			_assetsTextures = $"{assetsTableRoot}/Textures/";
			if (!Directory.Exists(_assetsTextures)) {
				Directory.CreateDirectory(_assetsTextures);
			}

			_assetsMaterials = $"{assetsTableRoot}/Materials/";
			if (!Directory.Exists(_assetsMaterials)) {
				Directory.CreateDirectory(_assetsMaterials);
			}
		}

		private void CreateRootHierarchy()
		{
			_tableGo = new GameObject(_table.Name);
			_playfieldGo = new GameObject("Playfield");
			var backglassGo = new GameObject("Backglass");
			var cabinetGo = new GameObject("Cabinet");

			_tableAuthoring = _tableGo.AddComponent<TableAuthoring>();
			_tableAuthoring.SetItem(_table);

			_playfieldGo.transform.SetParent(_tableGo.transform, false);
			backglassGo.transform.SetParent(_tableGo.transform, false);
			cabinetGo.transform.SetParent(_tableGo.transform, false);

			_playfieldGo.transform.localRotation = GlobalRotation;
			_playfieldGo.transform.localPosition = new Vector3(-_table.Width / 2 * GlobalScale, 0f, _table.Height / 2 * GlobalScale);
			_playfieldGo.transform.localScale = new Vector3(GlobalScale, GlobalScale, GlobalScale);
		}

		private GameObject GetGroupParent(IItem item)
		{
			// create group parent if not created (if null, attach it to the table directly).
			if (!string.IsNullOrEmpty(item.ItemGroupName)) {
				if (!_groupParents.ContainsKey(item.ItemGroupName)) {
					var parent = new GameObject(item.ItemGroupName);
					parent.transform.SetParent(_playfieldGo.transform, false);
					_groupParents[item.ItemGroupName] = parent;
				}
			}
			var groupParent = !string.IsNullOrEmpty(item.ItemGroupName)
				? _groupParents[item.ItemGroupName]
				: _playfieldGo;

			return groupParent;
		}
	}
}
