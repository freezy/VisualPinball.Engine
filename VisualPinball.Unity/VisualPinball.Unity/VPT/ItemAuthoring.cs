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

// ReSharper disable InconsistentNaming

using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The base class for all authoring components on the playfield.<p/>
	/// </summary>
	/// <typeparam name="TItem"></typeparam>
	public abstract class ItemAuthoring : MonoBehaviour
	{
		public abstract string ItemName { get; }

		protected static void DrawArrow(Vector3 pos, Vector3 direction, float arrowHeadLength = 0.025f, float arrowHeadAngle = 20.0f)
		{
			Debug.DrawRay(pos, direction);

			var right = Quaternion.LookRotation(direction) * Quaternion.Euler(0,180+arrowHeadAngle,0) * new Vector3(0,0,1);
			var left = Quaternion.LookRotation(direction) * Quaternion.Euler(0,180-arrowHeadAngle,0) * new Vector3(0,0,1);
			Debug.DrawRay(pos + direction, right * arrowHeadLength);
			Debug.DrawRay(pos + direction, left * arrowHeadLength);
		}

		protected void SetMaterialProperty<T>(int prop, float value) where T : MonoBehaviour {
			var child = GetComponentInChildren<T>();
			var mr = child != null ? child.GetComponent<MeshRenderer>() : null;
			if (mr != null) {
				mr.sharedMaterial.SetFloat(prop, value);
			}
		}
	}
}
