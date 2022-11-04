// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

namespace VisualPinball.Unity.Editor
{
	public class BoundingBoxComponent : MonoBehaviour
	{

		void OnDrawGizmosSelected()
		{
			Gizmos.matrix = Matrix4x4.identity;
			Gizmos.color = Color.yellow;
			var r = GetComponent<Renderer>();
			if (r != null) {
				var b = r.bounds;
				Gizmos.DrawSphere(b.center, 0.001f); //center sphere
				Gizmos.DrawWireCube(b.center, b.size);
			} else {
				var rs = GetComponentsInChildren<Renderer>();
				foreach (var r2 in rs) {
					var b = r2.bounds;
					Gizmos.DrawSphere(b.center, 0.001f); //center sphere
					Gizmos.DrawWireCube(b.center, b.size);
				}
			}
		}
	}
}
