using System.IO;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Editor
{
	public class VpxSceneConverter
	{
		private readonly Table _table;
		private GameObject _tableGo;
		private GameObject _playfieldGo;
		private string _assetsTextures;

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

			} finally {

				// resume asset database refreshing
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
			}

			return _tableGo;
		}

		private void ExtractTextures()
		{
			foreach (var texture in _table.Textures) {
				File.WriteAllBytes(texture.GetUnityFilename(_assetsTextures), texture.Content);
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

		}

		private void CreateRootHierarchy()
		{
			_tableGo = new GameObject {
				name = _table.Name
			};

			_playfieldGo = new GameObject {
				name = "Playfield"
			};

			var backglassGo = new GameObject {
				name = "Backglass"
			};

			var cabinetGo = new GameObject {
				name = "Cabinet"
			};

			_playfieldGo.transform.parent = _tableGo.transform;
			backglassGo.transform.parent = _tableGo.transform;
			cabinetGo.transform.parent = _tableGo.transform;
		}
	}
}
