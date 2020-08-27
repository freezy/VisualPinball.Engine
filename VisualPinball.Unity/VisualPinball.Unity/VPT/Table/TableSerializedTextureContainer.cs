using System;
using System.Collections.Generic;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Implements a serializable texture container that can replace the engine table container after import
	/// </summary>
	[Serializable]
	public class TableSerializedTextureContainer : TableSerializedContainer<Texture, TextureData, TableSerializedTexture>
	{
		protected override bool TryAddSerialized(Texture value)
		{
			_serializedData.Add(TableSerializedTexture.Create(value.Data));
			return true;
		}

		protected override Dictionary<string, Texture> CreateDict()
		{
			var dict = new Dictionary<string, Texture>();
			foreach (var td in _serializedData) {
				dict.Add(td.Data.Name.ToLower(), new Texture(td.Data));
			}
			_dictDirty = false;
			return dict;
		}
	}
}
