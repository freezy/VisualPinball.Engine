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

using UnityEngine;
using UnityEditor;

namespace VisualPinball.Unity.Editor.Utils
{
	/// <summary>
	/// This class is a helper to Frame the scene view using objects boundaries
	/// </summary>
	public class SceneViewFramer
	{
		public static void FrameObjects(Object[] objects, bool includeChildren = true, bool instant = false)
		{
			Bounds bounds = new Bounds();
			foreach (var obj in objects) {
				if (obj is UnityEngine.GameObject gameObj) {
					var renders = includeChildren ? gameObj.GetComponentsInChildren<Renderer>() : gameObj.GetComponents<Renderer>();
					foreach (var render in renders) {
						bounds.Encapsulate(render.bounds);
					}
				}
			}

			if (bounds.extents != Vector3.zero) {
				SceneView.lastActiveSceneView.Frame(bounds, instant);
			}
		}
	}
}
