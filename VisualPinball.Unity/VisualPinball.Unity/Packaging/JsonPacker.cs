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

using System;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public class JsonPacker : IDataPacker
	{
		private static readonly JsonSerializerSettings Settings = VpeJson.Settings(new JsonSerializerSettings {
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore
		});

		public byte[] Pack<T>(T obj)
		{
			return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj, Formatting.Indented, Settings));
		}

		public byte[] Empty => Pack(new object());

		public T Unpack<T>(byte[] data)
		{
			return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(data), Settings);
		}

		public void Unpack(byte[] data, object instance)
		{
			JsonConvert.PopulateObject(Encoding.UTF8.GetString(data), instance, Settings);
		}

		public object Unpack(Type t, byte[] data)
		{
			if (typeof(ScriptableObject).IsAssignableFrom(t)) {
				var instance = ScriptableObject.CreateInstance(t);
				JsonConvert.PopulateObject(Encoding.UTF8.GetString(data), instance, Settings);
				return instance;
			}
			return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(data), t, Settings);
		}

		public string FileExtension => ".json";
	}
}
