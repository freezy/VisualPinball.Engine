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

using Unity.Mathematics;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity
{
	public abstract class TargetColliderGenerator
	{
		private readonly IApiColliderGenerator _api;
		protected readonly ITargetData Data;
		protected readonly IMeshGenerator MeshGenerator;
		protected readonly float4x4 _matrix;

		protected TargetColliderGenerator(IApiColliderGenerator api, ITargetData data, IMeshGenerator meshGenerator, float4x4 matrix)
		{
			_api = api;
			Data = data;
			MeshGenerator = meshGenerator;
			_matrix = matrix;
		}

		private protected void GenerateCollidables(Mesh hitMesh, ref EdgeSet addedEdges, bool setHitObject, ref ColliderReference colliders)  {

			// add the normal drop target as collidable but without hit event
			for (var i = 0; i < hitMesh.Indices.Length; i += 3) {
				var i0 = hitMesh.Indices[i];
				var i1 = hitMesh.Indices[i + 1];
				var i2 = hitMesh.Indices[i + 2];

				// NB: HitTriangle wants CCW vertices, but for rendering we have them in CW order
				var rgv0 = hitMesh.Vertices[i0].ToUnityFloat3();
				var rgv1 = hitMesh.Vertices[i1].ToUnityFloat3();
				var rgv2 = hitMesh.Vertices[i2].ToUnityFloat3();

				colliders.Add(new TriangleCollider(rgv0, rgv2, rgv1, GetColliderInfo(setHitObject)));

				if (addedEdges.ShouldAddHitEdge(i0, i1)) {
					colliders.Add(new Line3DCollider(rgv0, rgv2, GetColliderInfo(setHitObject)));
				}
				if (addedEdges.ShouldAddHitEdge(i1, i2)) {
					colliders.Add(new Line3DCollider(rgv2, rgv1, GetColliderInfo(setHitObject)));
				}
				if (addedEdges.ShouldAddHitEdge(i2, i0)) {
					colliders.Add(new Line3DCollider(rgv1, rgv0, GetColliderInfo(setHitObject)));
				}
			}

			// add collision vertices
			foreach (var vertex in hitMesh.Vertices) {
				colliders.Add(new PointCollider(vertex.ToUnityFloat3(), GetColliderInfo(setHitObject)));
			}
		}

		protected ColliderInfo GetColliderInfo(bool setHitObject)
		{
			var info = _api.GetColliderInfo();
			info.FireEvents = setHitObject && info.FireEvents;
			return info;
		}
	}
}
