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

using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.Game
{
	public class RenderObject
	{
		public readonly string Name;
		public readonly Mesh Mesh;

		public readonly PbrMaterial Material;
		public readonly bool IsVisible;

		public RenderObject(string name, Mesh mesh, PbrMaterial material, bool isVisible)
		{
			Name = name;
			Mesh = mesh;
			Material = material;
			IsVisible = isVisible;
		}
	}
}
