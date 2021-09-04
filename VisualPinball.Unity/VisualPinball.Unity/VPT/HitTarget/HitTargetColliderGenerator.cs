using System.Collections.Generic;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity
{
	public class HitTargetColliderGenerator : TargetColliderGenerator
	{
		public HitTargetColliderGenerator(IApiColliderGenerator hitTargetApi, ITargetData data, IMeshGenerator meshProvider) : base(hitTargetApi, data, meshProvider)
		{
		}

		internal void GenerateColliders(List<ICollider> colliders)
		{
			var localToPlayfield = MeshGenerator.GetTransformationMatrix();
			var hitMesh = MeshGenerator.GetMesh();
			for (var i = 0; i < hitMesh.Vertices.Length; i++) {
				hitMesh.Vertices[i].MultiplyMatrix(localToPlayfield);
			}
			var addedEdges = EdgeSet.Get();
			GenerateCollidables(hitMesh, addedEdges, true, colliders);
		}
	}
}
