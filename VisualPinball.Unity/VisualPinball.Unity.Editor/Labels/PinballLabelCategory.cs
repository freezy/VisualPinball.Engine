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
using System.Diagnostics;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[DebuggerDisplay("Name = {Name}, MultipleSelection = {MultipleSelection}, Color = {Color}")]
	[Serializable]
	public class PinballLabelCategory 
	{
		public static readonly char Separator = '_';

		[NonSerialized]
		public static readonly Color DefaultColor = Color.blue;

		[SerializeField]
		public string Name = string.Empty;

		[SerializeField]
		public bool MultipleSelection = true;

		[SerializeField]
		public Color Color = DefaultColor;

		public PinballLabelCategory()
		{
		}

		public PinballLabelCategory(PinballLabelCategory category)
		{
			Name = category.Name;
			MultipleSelection = category.MultipleSelection;
			Color = category.Color;
		}
	}
}
