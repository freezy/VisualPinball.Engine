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

using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// AssetLibraryContent will host all populated assets from a <see cref="AssetsLibrarySettingsAsset"/>
	/// </summary>
	public class AssetLibraryContent
	{
		/// <summary>
		/// Within the <see cref="AssetLibraryContent"/>, each AssetLibraryFolderContent will host assets from each <see cref="AssetsLibrarySettingsAsset.FolderSettings"/>
		/// It'll also watch for any changes within this folder to automatically update assets' list.
		/// </summary>
		public class AssetLibraryFolderContent
		{
			private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();

			public class AssetData
			{
				public Object Asset;
				public string[] Labels;
			}

			public AssetsLibrarySettingsAsset.FolderSettings FolderSetting;
			public List<AssetData> Assets = new List<AssetData>();
			private FileSystemWatcher FolderWatcher = new FileSystemWatcher();
			private string Types;

			public AssetLibraryFolderContent(AssetsLibrarySettingsAsset.FolderSettings folderSetting, List<string> types)
			{
				Types = string.Join(" ", types.Select(T => $"t:{T}").ToArray());
				FolderSetting = folderSetting;
				var folderPath = AssetDatabase.GUIDToAssetPath(FolderSetting.FolderReference.Guid);
				if (folderPath != string.Empty) {
					FolderWatcher.Path = folderPath;
					FolderWatcher.NotifyFilter = NotifyFilters.Attributes
										 | NotifyFilters.CreationTime
										 | NotifyFilters.DirectoryName
										 | NotifyFilters.FileName
										 | NotifyFilters.LastAccess
										 | NotifyFilters.LastWrite
										 | NotifyFilters.Security
										 | NotifyFilters.Size;
					FolderWatcher.Created += OnChanged;
					FolderWatcher.Deleted += OnChanged;
					FolderWatcher.Renamed += OnRenamed;
					FolderWatcher.Changed += OnChanged;
					FolderWatcher.EnableRaisingEvents = true;
				}
			}

			private void OnChanged(object sender, FileSystemEventArgs e)
			{
				//TODO
			}

			private void OnRenamed(object sender, RenamedEventArgs e)
			{
				//TODO
			}

			public void PopulateAssets()
			{
				Assets.Clear();
				if (string.IsNullOrEmpty(Types)) return;
				var folderpath = AssetDatabase.GUIDToAssetPath(FolderSetting.FolderReference.Guid);
				EditorUtility.DisplayProgressBar("Parsing Assets", folderpath, 0.0f);
				var guids = AssetDatabase.FindAssets(Types, new[] { folderpath });
				int count = 0;

				foreach (var guid in guids) {
					var path = AssetDatabase.GUIDToAssetPath(guid);
					if (!FolderSetting.Recursive && !Path.GetDirectoryName(path).Equals(folderpath, StringComparison.InvariantCultureIgnoreCase)) {
						continue;
					}
					var asset = AssetDatabase.LoadMainAssetAtPath(path.Replace("\\", "/"));
					if (asset != null) {
						//var cachedMetadata = PinballMetadataCache.LoadMetadata(guid);
						var assetData = new AssetData() { Asset = asset, Labels = AssetDatabase.GetLabels(new GUID(guid)) };
						Assets.Add(assetData);
					}
					EditorUtility.DisplayProgressBar($"Parsing Assets {count + 1}/{guids.Length}", path, (float)count++ / guids.Length);
				}

				EditorUtility.ClearProgressBar();
			}
		}

		public List<AssetLibraryFolderContent> FolderContents { get; private set; } = new List<AssetLibraryFolderContent>();

		public AssetsLibrarySettingsAsset Settings { get; private set; }

		public AssetLibraryContent(AssetsLibrarySettingsAsset settings)
		{
			Settings = settings;
		}

		public Action<AssetLibraryFolderContent> FolderPopulate;

		public void PopulateAssets()
		{
			foreach (var folder in Settings.Folders) {
				if (!FolderContents.Any(FC => FC.FolderSetting == folder)) {
					var newFolder = new AssetLibraryFolderContent(folder, Settings.AssetTypes);
					newFolder.PopulateAssets();
					FolderPopulate?.Invoke(newFolder);
					FolderContents.Add(newFolder);
				}
			}
		}
	}
}
