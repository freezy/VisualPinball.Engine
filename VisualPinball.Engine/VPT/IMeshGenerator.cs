// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// Provide a mesh and transformation matrix without knowing the source.
	/// </summary>
	///
	/// <remarks>
	/// The main goal of this interface is a way to abstract mesh generation in
	/// cases where the same mesh is provided by different sources. One case
	/// are primitives, where the original mesh is provided by the core mesh
	/// generator, but collision later needs the same mesh, which is then provided
	/// through the MeshFilter of the gameObject.
	/// </remarks>
	public interface IMeshGenerator
	{
		string name { get; }

		Mesh GetMesh(); // assuming: Origin.Original, false
	}
}
