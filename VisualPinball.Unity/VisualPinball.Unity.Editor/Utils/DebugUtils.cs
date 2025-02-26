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

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	public class DebugUtils
	{
		[MenuItem("GameObject/Pinball/Debug/Reset Surface Transformations", false, 12)]
		public static void ResetTransformations()
		{
			if (Selection.activeGameObject == null) {
				return;
			}
			var gameObjects = Selection.activeGameObject
				.GetComponentsInChildren<ISurfaceComponent>()
				.Select(comp => (comp as MonoBehaviour)?.gameObject)
				.ToArray();

			Undo.RecordObjects(gameObjects, "Reset Surface Transformations");
			foreach (var go in gameObjects) {
				go.transform.localRotation = Quaternion.identity;
				go.transform.localScale = Vector3.one;
			}
		}
	}
}
