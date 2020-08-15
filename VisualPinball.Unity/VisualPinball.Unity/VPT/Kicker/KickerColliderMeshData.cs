using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public struct ColliderMeshData : IComponentData
	{
		public BlobAssetReference<KickerMeshVertexBlobAsset> Value;
	}

	public struct KickerMeshVertexBlobAsset
	{
		public BlobArray<KickerMeshVertex> Vertices;
		public BlobArray<KickerMeshVertex> Normals;
	}

	public struct KickerMeshVertex
	{
		public float3 Vertex;
	}
}
