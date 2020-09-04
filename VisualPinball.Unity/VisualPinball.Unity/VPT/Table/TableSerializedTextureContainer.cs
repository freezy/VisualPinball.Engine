// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
