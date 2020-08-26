using System;
using System.Collections;
using System.Collections.Generic;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.VPT.Table
{
	/// <summary>
	/// Implements a serializable texture container that can replace the engine table container after import
	/// </summary>
	[Serializable]
	public class TableSerializedTextureContainer : TableSerializedContainer<Texture, TextureData, TableSerializedTexture>
	{
		public override void Add(Texture value)
		{
			Remove(value);
			string lowerName = value.Name.ToLower();
			_serializedData.Add(TableSerializedTexture.Create(value.Data));
			Data[lowerName] = value;
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
