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

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// This class manage the Json serialization of a polymorphic list.
	/// It outputs each list element type alongside its Json export to ensure correct serialization.
	/// </summary>
	[Serializable]
	public class JsonPolymorphicList<BaseType> : ISerializationCallbackReceiver
	{
		[NonSerialized]
		public List<BaseType> Items = new List<BaseType>();

		[Serializable]
		class SerializedItem
		{
			public string Type;
			public string Json;
		}

		[SerializeField]
		private List<SerializedItem> SerializedItems = new List<SerializedItem>();

		public void OnBeforeSerialize()
		{
			SerializedItems.Clear();
			foreach(var item in Items) {
				SerializedItems.Add(new SerializedItem() {
					Type = item.GetType().ToString(),
					Json = JsonUtility.ToJson(item)
				});
			}
		}

		public void OnAfterDeserialize()
		{
			foreach (var item in SerializedItems) {
				Type type = Type.GetType(item.Type);
				if (type.IsSubclassOf(typeof(BaseType))) {
					Items.Add((BaseType)JsonUtility.FromJson(item.Json, type));
				}
			}
			SerializedItems.Clear();
		}

	}
}
