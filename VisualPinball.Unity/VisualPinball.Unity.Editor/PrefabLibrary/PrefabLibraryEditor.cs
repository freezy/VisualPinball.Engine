using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using Logger = NLog.Logger;
using Object = UnityEngine.Object;
using System.Linq;
using System.IO;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{


	internal class PrefabLibraryFolderContent
	{
		private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();

		public class PrefabData
		{
			public GameObject Prefab;
			public PinballTagsMetadata TagsMetadata;
		}

		public PrefabLibrarySettingsAsset.FolderSettings FolderSetting;
		public List<PrefabData> Prefabs = new List<PrefabData>();
		private FileSystemWatcher FolderWatcher = new FileSystemWatcher();
		private List<string> TagsCategories = new List<string>();

		public List<string> Categories => TagsCategories;

		public List<string> Tags {
			get {
				var tags = new List<string>();
				foreach (var prefab in Prefabs) {
					if (prefab.TagsMetadata != null) {
						tags = tags.Union(prefab.TagsMetadata.Tags).ToList();
					}
				}
				return tags;
			}
		}

		public Action<PrefabLibraryFolderContent> Populate;

		public PrefabLibraryFolderContent(PrefabLibrarySettingsAsset.FolderSettings folderSetting)
		{
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

		}

		private void OnRenamed(object sender, RenamedEventArgs e)
		{
			
		}

		public void PopulatePrefabs()
		{
			Prefabs.Clear();
			var folderpath = AssetDatabase.GUIDToAssetPath(FolderSetting.FolderReference.Guid);
			EditorUtility.DisplayProgressBar("Parsing Prefabs", folderpath, 0.0f);
			var guids = AssetDatabase.FindAssets("t: Prefab", new[] { folderpath });
			int count = 0;

			foreach (var guid in guids) {
				var path = AssetDatabase.GUIDToAssetPath(guid);
				if (!FolderSetting.Recursive && !Path.GetDirectoryName(path).Equals(folderpath, StringComparison.InvariantCultureIgnoreCase)) {
					continue;
				}
				var asset = AssetDatabase.LoadMainAssetAtPath(path.Replace("\\", "/")) as GameObject;
				if (asset != null) {
					var cachedMetadata = PinballMetadataCache.LoadMetadata(guid);
					var prefabData = new PrefabData() { Prefab = asset, TagsMetadata = cachedMetadata.GetExtension<PinballTagsMetadata>() };
					Prefabs.Add(prefabData);
				}
				EditorUtility.DisplayProgressBar($"Parsing Prefabs {count+1}/{guids.Length}", path, (float)count++/guids.Length);
			}

			EditorUtility.ClearProgressBar();

			//Extract Categories
			TagsCategories.Clear();
			foreach (var prefab in Prefabs) {
				if (prefab.TagsMetadata != null) {
					foreach (var tag in prefab.TagsMetadata.Tags) {
						var subtags = tag.Split('.');
						if (subtags.Length > 1 && !TagsCategories.Contains(subtags[0])) {
							TagsCategories.Add(subtags[0]);
						}
					}
				}
			}

			Populate?.Invoke(this);
		}
	}

	public class PrefabLibraryEditor : BaseEditorWindow
	{
		private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();

		private List<PrefabLibraryFolderContent> FolderContents = new List<PrefabLibraryFolderContent>();
		private List<string> Tags = new List<string>();
		private List<string> Categories = new List<string>();

		[MenuItem("Visual Pinball/Prefab Library", false, 601)]
		public static void ShowWindow()
		{
			GetWindow<PrefabLibraryEditor>().titleContent = new GUIContent("Prefabs Library");
		}

		public override void OnEnable()
		{
			base.OnEnable();

			VisualTreeAsset original = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/PrefabLibrary/PrefabLibraryEditor.uxml");
			TemplateContainer treeAsset = original.CloneTree();
			rootVisualElement.Add(treeAsset);
		}

		public void OnGUI()
		{
			CheckPrefabSettinbgsAssets();
		}

		private void CheckPrefabSettinbgsAssets()
		{
			PinballMetadataCache.ClearCache();
			var guids = AssetDatabase.FindAssets("t: PrefabLibrarySettingsAsset");
			foreach(var guid in guids) {
				var asset = AssetDatabase.LoadAssetAtPath<PrefabLibrarySettingsAsset>(AssetDatabase.GUIDToAssetPath(guid));
				foreach (var folder in asset.Folders) {
					if (!FolderContents.Any(FC=>FC.FolderSetting == folder)) {
						var newFolder = new PrefabLibraryFolderContent(folder);
						newFolder.Populate += OnFolderPopulate;
						newFolder.PopulatePrefabs();
						FolderContents.Add(newFolder);
					}
				}
				Categories = Categories.Union(asset.Categories).ToList();
				Tags = Tags.Union(asset.AvailableTags).ToList();
			}
		}

		private void OnFolderPopulate(PrefabLibraryFolderContent folder)
		{
			Categories = Categories.Union(folder.Categories).ToList();
			Tags = Tags.Union(folder.Tags).ToList();
		}
	}
}
