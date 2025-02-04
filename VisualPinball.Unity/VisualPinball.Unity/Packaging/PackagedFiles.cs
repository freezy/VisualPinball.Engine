// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using UnityEngine;
using VisualPinball.Unity.Editor.Packaging;

namespace VisualPinball.Unity.Packaging
{
	public class PackagedFiles
	{
		private readonly HashSet<ScriptableObject> _scriptableObjects = new();

		private readonly IPackageFolder _tableFolder;

		public PackagedFiles(IPackageFolder tableFolder)
		{
			_tableFolder = tableFolder;
		}

		public int AddAsset(ScriptableObject scriptableObject)
		{
			if (scriptableObject == null) {
				return 0;
			}
			_scriptableObjects.Add(scriptableObject);
			return scriptableObject.GetInstanceID();
		}

		public void PackAssets()
		{
			if (_scriptableObjects.Count == 0) {
				return;
			}
			var assetFolder = _tableFolder.AddFolder(PackageApi.AssetFolder);
			foreach (var so in _scriptableObjects) {
				var subFolder = so.GetType().Name;
				if (!assetFolder.TryGetFolder(subFolder, out var assetTypeFolder)) {
					assetTypeFolder = assetFolder.AddFolder(subFolder);
				}
				var name = UniqueName(assetTypeFolder, so.name);
				var file = assetTypeFolder.AddFile(name, PackageApi.Packer.FileExtension);
				file.SetData(ScriptableObjectPackable.Pack(so));
			}
		}

		private string UniqueName(IPackageFolder folder, string name)
		{
			var baseName = name;
			var i = 1;
			while (true) {
				if (folder.TryGetFile(name, out _, PackageApi.Packer.FileExtension)) {
					name = $"{baseName}_({++i})";
				} else {
					return name;
				}
			}
		}
	}
}
