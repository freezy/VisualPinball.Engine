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

using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity
{
	public class HitTargetColliderGenerator : TargetColliderGenerator
	{
		public HitTargetColliderGenerator(IApiColliderGenerator api, ITargetData data, IMeshGenerator meshProvider, float4x4 matrix)
			: base(api, data, meshProvider, matrix) { }

		internal void GenerateColliders(ref ColliderReference colliders)
		{
			var localToPlayfield = MeshGenerator.GetTransformationMatrix();
			var hitMesh = MeshGenerator.GetMesh();
			for (var i = 0; i < hitMesh.Vertices.Length; i++) {
				hitMesh.Vertices[i].MultiplyMatrix(localToPlayfield);
			}
			var addedEdges = EdgeSet.Get(Allocator.TempJob);
			GenerateCollidables(hitMesh, ref addedEdges, true, ref colliders);
			addedEdges.Dispose();
		}
	}
}
