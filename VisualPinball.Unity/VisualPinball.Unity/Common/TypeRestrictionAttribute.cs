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
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Shows an object picker based on an interface.
	/// </summary>
	/// <a href="https://forum.unity.com/threads/creating-assignable-c-interface-fields-in-the-inspector.170195/#post-3521067">Forum Post</a>
	public class TypeRestrictionAttribute : PropertyAttribute
	{
		public readonly Type Type;
		public string PickerLabel = "Pick Item";
		public string NoneLabel = "None";
		public bool UpdateTransforms;
		public bool RebuildMeshes;
		public string DeviceItem;
		public Type DeviceType;

		public TypeRestrictionAttribute(Type type)
		{
			Type = type ?? throw new ArgumentNullException(nameof(type), "Type must be given!");
		}

	}
}
