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
	/// Put this attribute above any field
	/// It adds a small label to the right of the property field.
	/// This small label can be used to add information about the assumed unit.
	///
	/// <code>
	/// <para>[Unit("m/s²")]</para>
	/// <para>public float gravity = 9.80665f;</para>
	/// </code>
	/// </summary>
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
	public class UnitAttribute : PropertyAttribute
	{
		public string label;
		public GUIStyle labelStyle;
		public float width;

		public UnitAttribute(string label)
		{
			this.label = label;
			labelStyle = GUI.skin.GetStyle("miniLabel");
			width = labelStyle.CalcSize(new GUIContent(label)).x;
		}
	}
}
