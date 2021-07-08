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
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Patcher
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
	public abstract class ItemMatchAttribute : Attribute
	{
		/// <summary>
		/// If set, pass the game object with this name as reference to the patcher.
		/// </summary>
		public string Ref;

		public abstract bool Matches(TableContainer th, IRenderable item, GameObject obj);
	}
}
