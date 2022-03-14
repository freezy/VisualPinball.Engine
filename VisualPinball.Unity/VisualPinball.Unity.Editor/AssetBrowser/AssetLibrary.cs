// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

using System.IO;
using LiteDB;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CreateAssetMenu(fileName = "Library", menuName = "Visual Pinball/Asset Library", order = 300)]
	public class AssetLibrary : ScriptableObject, ISerializationCallbackReceiver
	{
		public string Name;

		public string LibraryRoot;

		public string _dbPath {
			get {
				var thisPath = AssetDatabase.GetAssetPath(this);
				return Path.GetDirectoryName(thisPath) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(thisPath) + ".db";
			}
		}

		public void Test()
		{
			Debug.Log($"DB PATH = {_dbPath}");
			// using (var db = new LiteDatabase(@"MyData.db")) {
			//
			// }
			// var col = db.GetCollection<Customer>("customers");
		}
		public void OnBeforeSerialize()
		{
			if (string.IsNullOrEmpty(LibraryRoot)) {
				LibraryRoot = Path.GetDirectoryName(AssetDatabase.GetAssetPath(this));
			}
		}

		public void OnAfterDeserialize()
		{
		}
	}

	public class LibraryAsset
	{
		public string Guid { get; set; }

	}
}
