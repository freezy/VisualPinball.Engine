using UnityEngine;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Extensions
{
	public static class Math
	{
		public static Vector3 ToUnityVector3(this Vertex3D vertex)
		{
			return new Vector3(vertex.X, vertex.Y, vertex.Z);
		}

		public static Vector3 ToUnityVector3(this Vertex2D vertex, float z)
		{
			return new Vector3(vertex.X, vertex.Y, z);
		}

		public static Vector3 ToUnityVector3(this Vertex3DNoTex2 vertex)
		{
			return new Vector3(vertex.X, vertex.Y, vertex.Z);
		}

		public static Vector3 ToUnityNormalVector3(this Vertex3DNoTex2 vertex)
		{
			return new Vector3(vertex.Nx, vertex.Ny, vertex.Nz);
		}

		public static Vector3 ToUnityUvVector2(this Vertex3DNoTex2 vertex)
		{
			return new Vector2(vertex.Tu, -vertex.Tv);
		}
	}
}
