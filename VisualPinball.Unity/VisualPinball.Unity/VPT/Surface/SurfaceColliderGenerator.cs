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

// ReSharper disable CompareOfFloatsByEqualityOperator

using Unity.Mathematics;
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity
{
	public class SurfaceColliderGenerator
	{
		private readonly IApiColliderGenerator _api;
		private readonly float4x4 _matrix;
		private readonly SurfaceMeshGenerator _meshGen;

		public SurfaceColliderGenerator(SurfaceApi surfaceApi, SurfaceComponent component, float4x4 matrix)
		{
			_api = surfaceApi;
			_matrix = matrix;

			var data = new SurfaceData();
			component.CopyDataTo(data, null, null, false);
			_meshGen = new SurfaceMeshGenerator(data);
		}

		internal void GenerateColliders(ref ColliderReference colliders)
		{
			var topMesh = _meshGen.GetMesh(SurfaceMeshGenerator.Top, 0, 0, 0, false);
			var sideMesh = _meshGen.GetMesh(SurfaceMeshGenerator.Side, 0, 0, 0, false);

			ColliderUtils.GenerateCollidersFromMesh(topMesh, _api.GetColliderInfo(), ref colliders, _matrix);
			ColliderUtils.GenerateCollidersFromMesh(sideMesh, _api.GetColliderInfo(), ref colliders, _matrix);
		}
	}
}
