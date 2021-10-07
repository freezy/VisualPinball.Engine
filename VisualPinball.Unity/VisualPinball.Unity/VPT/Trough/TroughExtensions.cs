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

using UnityEngine;
using VisualPinball.Engine.VPT.Trough;

namespace VisualPinball.Unity.Editor
{
	public static class TroughExtensions
	{
		#if UNITY_EDITOR

		public static GameObject InstantiateEditorPrefab(this Trough trough, Transform parent)
		{
			var prefab = Resources.Load<GameObject>("Prefabs/Trough");
			var troughGo = UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
			if (!troughGo) {
				return troughGo;
			}
			var troughComponent = troughGo.GetComponent<TroughComponent>();
			troughComponent.SetData(trough.Data);
			return troughGo;
		}

		#endif
	}
}
