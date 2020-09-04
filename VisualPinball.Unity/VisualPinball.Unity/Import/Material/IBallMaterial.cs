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

using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Common interface for ball material for the various render pipelines
	/// </summary>
	public interface IBallMaterial
	{
		/// <summary>
		/// Get the shader for the currently detected graphics pipeline.
		/// </summary>
		/// <returns></returns>
		Shader GetShader();

		/// <summary>
		/// Create a ball material for the currently detected graphics pipeline.
		/// </summary>
		/// <param name="vpxMaterial"></param>
		/// <param name="table"></param>
		/// <param name="debug"></param>
		/// <returns></returns>
		Material CreateMaterial();
	}
}
