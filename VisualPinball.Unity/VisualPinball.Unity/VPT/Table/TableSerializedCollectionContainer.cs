using System;
using System.Collections.Generic;
using VisualPinball.Engine.VPT.Collection;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Implements a serializable texture container that can replace the engine table container after import
	/// </summary>
	[Serializable]
	public class TableSerializedCollectionContainer : TableSerializedContainer<Collection, CollectionData, TableSerializedCollection>
	{
		public override void Add(Collection value)
		{
			Remove(value);
			string lowerName = value.Name.ToLower();
			_serializedData.Add(TableSerializedCollection.Create(value.Data));
			Data[lowerName] = value;
		}

		protected override Dictionary<string, Collection> CreateDict()
		{
			var dict = new Dictionary<string, Collection>();
			foreach (var td in _serializedData) {
				dict.Add(td.Data.Name.ToLower(), new Collection(td.Data));
			}
			_dictDirty = false;
			return dict;
		}
	}
}
