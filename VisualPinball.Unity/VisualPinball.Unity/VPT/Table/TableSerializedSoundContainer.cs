using System;
using System.Collections;
using System.Collections.Generic;
using VisualPinball.Engine.VPT.Sound;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Implements a serializable Sound container that can replace the engine table container after import
	/// </summary>
	[Serializable]
	public class TableSerializedSoundContainer : TableSerializedContainer<Sound, SoundData, TableSerializedSound>
	{
		protected override bool TryAddSerialized(Sound value)
		{
			_serializedData.Add(TableSerializedSound.Create(value.Data));
			return true;
		}

		protected override Dictionary<string, Sound> CreateDict()
		{
			var dict = new Dictionary<string, Sound>();
			foreach (var td in _serializedData) {
				dict.Add(td.Data.Name.ToLower(), new Sound(td.Data));
			}
			_dictDirty = false;
			return dict;
		}
	}
}
