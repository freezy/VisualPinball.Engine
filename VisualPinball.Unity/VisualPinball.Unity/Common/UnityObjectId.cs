// Visual Pinball Engine
//
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without
// even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// General Public License for more details.
//
// You should have received a copy of the GNU General Public License along with this program. If not,
// see <https://www.gnu.org/licenses/>.

namespace VisualPinball.Unity
{
	public static class UnityObjectId
	{
#if UNITY_6000_5_OR_NEWER
		private static readonly object Lock = new object();
		private static readonly System.Collections.Generic.Dictionary<UnityEngine.EntityId, int> EntityIds = new System.Collections.Generic.Dictionary<UnityEngine.EntityId, int>();
		private static int _nextId = 1;
#endif

		public static int Get(UnityEngine.Object obj)
		{
#if UNITY_6000_5_OR_NEWER
			var entityId = obj.GetEntityId();
			lock (Lock) {
				if (EntityIds.TryGetValue(entityId, out var id)) {
					return id;
				}
				id = _nextId++;
				EntityIds.Add(entityId, id);
				return id;
			}
#else
			return obj.GetInstanceID();
#endif
		}
	}
}
