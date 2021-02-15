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

namespace VisualPinball.Engine.IO
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class BiffIgnoreAttribute : Attribute
	{
		public readonly string Name;

		/// <summary>
		/// If set, marks this as "deprecated in VP", which still writes it when the WRITE_VP106 or WRITE_VP107 flag is set.
		/// </summary>
		public bool IsDeprecatedInVP;

		public BiffIgnoreAttribute(string name)
		{
			Name = name;
		}
	}
}
