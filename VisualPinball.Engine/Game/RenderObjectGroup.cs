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

using System;
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

		public readonly string ComponentName;
		public readonly ItemSubComponent SubComponent;
		public readonly string SubName;

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
			(ComponentName, SubComponent, SubName) = SplitName();
		}

		public enum ItemSubComponent
		{
			None, Collider, Mesh
		}

		private (string, ItemSubComponent, string) SplitName()
		{
			var names = Name.Split(new[] {'_'}, 3, StringSplitOptions.None);
			if (names.Length == 1) {
				return (Name, ItemSubComponent.None, null);
			}
			switch (names[1].ToLower()) {
				case "collider":
					return (names[0], ItemSubComponent.Collider, names.Length > 2 ? names[2] : null);

				case "mesh":
					return (names[0], ItemSubComponent.Mesh, names.Length > 2 ? names[2] : null);

				default:
					return (Name, ItemSubComponent.None, null);
			}
		}
	}
}
