using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[Serializable]
	public class PinballMetadataExtension
	{
		public int Version = 1;
	}

	[Serializable]
	public class PinballTagsMetadata : PinballMetadataExtension
	{
		public PinballTagsMetadata() : base()
		{
			Version = 2;
		}

		public List<string> Tags = new List<string>();
	}

	[Serializable]
	public class PinballMetadata
	{
		[SerializeField]
		private JsonPolymorphicList<PinballMetadataExtension> Extensions = new JsonPolymorphicList<PinballMetadataExtension>();

		public void AddExtenstion(PinballMetadataExtension ext)
		{
			Remove(ext.GetType());
			Extensions.Items.Add(ext);
		}

		public void Remove(Type extType)
		{
			Extensions.Items.RemoveAll(E => E.GetType() == extType);
		}
		public ExtType GetExtension<ExtType>() where ExtType : PinballMetadataExtension
		{
			return (ExtType)Extensions.Items.FirstOrDefault(E => E.GetType() == typeof(ExtType));
		}
	}

	public class PinballMetadataCache : Dictionary<string, PinballMetadata>
	{
		public static PinballMetadata GetMetadata(string guid) => PinballMetadataMainCache.FirstOrDefault(KV => KV.Key.Equals(guid, StringComparison.InvariantCultureIgnoreCase)).Value;
		public static void AddMetadata(string guid, PinballMetadata data) {
			if (PinballMetadataMainCache.ContainsKey(guid)) {
				PinballMetadataMainCache[guid] = data;
			} else {
				PinballMetadataMainCache.Add(guid, data);
			}
		}

		public static void RemoveMetaData(string guid)
		{
			PinballMetadataMainCache.Remove(guid);
		}

		public static void ClearCache()
		{
			PinballMetadataMainCache.Clear();
		}

		public static PinballMetadata LoadMetadata(string guid)
		{
			var path = AssetDatabase.GUIDToAssetPath(guid);
			var metadata = GetMetadata(guid);
			if (metadata == null) {
				metadata = new PinballMetadata();
				AddMetadata(guid, metadata);
				if (path != "") {
					path = path.Replace("\\", "/");
					AssetImporter import = AssetImporter.GetAtPath(path);
					if (import.userData != string.Empty) {
						EditorJsonUtility.FromJsonOverwrite(import.userData, metadata);
					}
				}
			}
			return metadata;
		}

		private static PinballMetadataCache PinballMetadataMainCache = new PinballMetadataCache();

	}
}
