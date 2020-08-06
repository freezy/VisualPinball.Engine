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
	public class TableSerializedTextureContainer : ITableResourceContainer<Texture>
	{
		public int Count => _textureData.Count;
		public IEnumerable<Texture> Values => Textures.Values;
		public IEnumerable<TableSerializedTexture> SerializedObjects => _textureData;

		[UnityEngine.SerializeField] private List<TableSerializedTexture> _textureData = new List<TableSerializedTexture>();
		[UnityEngine.SerializeField] private bool _textureDictDirty = false;
		private Dictionary<string, Texture> Textures => _textures == null || _textureDictDirty ? (_textures = CreateTextureDict()) : _textures;
		private Dictionary<string, Texture> _textures = null;

		public Texture this[string k] => Get(k);
		public Texture Get(string k)
		{
			Textures.TryGetValue(k.ToLower(), out Texture val);
			return val;
		}

		public void Add(Texture value)
		{
			Remove(value);
			string lowerName = value.Name.ToLower();
			_textureData.Add(TableSerializedTexture.Create(value.Data));
			Textures[lowerName] = value;
		}

		public bool Remove(Texture value)
		{
			return Remove(value.Name);
		}

		public bool Remove(string name)
		{
			string lowerName = name.ToLower();
			bool found = false;
			for (int i = 0; i < _textureData.Count; i++) {
				if (_textureData[i].Data.Name.ToLower() == lowerName) {
					_textureData.RemoveAt(i);
					found = true;
					break;
				}
			}
			Textures.Remove(lowerName);
			return found;
		}

		public void SetNameMapDirty()
		{
			_textureDictDirty = true;
		}

		private Dictionary<string, Texture> CreateTextureDict()
		{
			var dict = new Dictionary<string, Texture>();
			foreach (var td in _textureData) {
				dict.Add(td.Data.Name.ToLower(), new Texture(td.Data));
			}
			_textureDictDirty = false;
			return dict;
		}

		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
		public IEnumerator<Texture> GetEnumerator()
		{
			foreach (var kvp in Textures) {
				yield return kvp.Value;
			}
		}
	}
}
