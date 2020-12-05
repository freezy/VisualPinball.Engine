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

using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Patcher
{
	/// <summary>
	/// Common methods for patching a table during import.
	/// </summary>
	static class PatcherUtil
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Hide the gameobject.
		/// </summary>
		/// <param name="gameObject"></param>
		public static void Hide(GameObject gameObject)
		{
			gameObject.SetActive(false);
		}

		/// <summary>
		/// Set a new parent for the given child while keeping the position and rotation.
		/// </summary>
		/// <param name="child"></param>
		/// <param name="parent"></param>
		public static void Reparent(GameObject child, GameObject parent)
		{
			var rot = child.transform.rotation;
			var pos = child.transform.position;

			// re-parent the child
			child.transform.SetParent(parent.transform, false);

			child.transform.rotation = rot;
			child.transform.position = pos;
		}
	}
}
