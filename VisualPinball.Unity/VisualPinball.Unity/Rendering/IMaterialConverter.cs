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
using System.Text;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Flipper;
using Material = UnityEngine.Material;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Common interface for material conversion with the various render pipelines
	/// </summary>
	public interface IMaterialConverter
	{
		/// <summary>
		/// Create a material for the currently detected graphics pipeline.
		/// </summary>
		/// <param name="vpxMaterial"></param>
		/// <param name="table"></param>
		/// <param name="objectType">Type of the item to which the material is applied (e.g. <see cref="Flipper"/>)</param>
		/// <param name="debug"></param>
		/// <returns></returns>
		Material CreateMaterial(PbrMaterial vpxMaterial, TableAuthoring table, Type objectType, StringBuilder debug = null);

	}
}
