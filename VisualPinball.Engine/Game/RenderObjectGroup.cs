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

using System.Linq;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.Game
{
	public class RenderObjectGroup
	{
		public readonly string Name;
		/// <summary>
		/// Name of the game item group this item is added under (e.g. "Flippers", "Walls", etc)
		/// </summary>
		public readonly string Parent;
		public readonly RenderObject[] RenderObjects;
		public readonly Matrix3D TransformationMatrix;

		public bool ForceChild { get; set; }
		public bool HasOnlyChild => RenderObjects.Length == 1;
		public bool HasChildren => RenderObjects.Length > 0;

		public RenderObject Get(string name) => RenderObjects.First(ro => ro.Name == name);

		public RenderObjectGroup(string name, string parent, Matrix3D matrix, params RenderObject[] renderObjects)
		{
			Name = name;
			Parent = parent;
			RenderObjects = renderObjects;
			TransformationMatrix = matrix;
		}
	}
}
