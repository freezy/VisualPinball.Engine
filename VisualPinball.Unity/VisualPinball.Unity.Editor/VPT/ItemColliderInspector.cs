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

namespace VisualPinball.Unity.Editor
{
	public class ItemColliderInspector<TAuthoring> : ItemInspector where TAuthoring : MonoBehaviour
	{
		protected TAuthoring GetAuthoring()
		{
			var mb = target as MonoBehaviour;
			if (mb == null) {
				return null;
			}
			var go = mb.gameObject;

			var auth = go.GetComponent<TAuthoring>();
			if (auth == null && go.transform.parent != null) {
				auth = go.transform.parent.GetComponent<TAuthoring>();
			}

			if (auth == null && go.transform.parent.transform.parent != null) {
				auth = go.transform.parent.transform.parent.GetComponent<TAuthoring>();
			}

			if (auth == null) {
				Debug.LogWarning("No parent rubber authoring component found.");
			}
			return auth;
		}
	}
}
