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

using System;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Patcher
{
	/// <summary>
	/// Matches an game item by its name.
	/// </summary>
	public class NameMatchAttribute : ItemMatchAttribute
	{
		public bool IgnoreCase = true;

		private readonly string _name;

		public NameMatchAttribute(string name)
		{
			_name = name;
		}

		public override bool Matches(FileTableContainer tableContainer, GameObject go)
		{
			if (!go) {
				return false;
			}
			return IgnoreCase
				? string.Equals(go.name, _name, StringComparison.CurrentCultureIgnoreCase)
				: go.name == _name;
		}
	}
}
