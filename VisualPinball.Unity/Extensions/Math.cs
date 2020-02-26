using Unity.Mathematics;
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

		public static float3 ToEuler(this quaternion quaternion) {
			var q = quaternion.value;
			double3 res;

			var sinRCosP = +2.0 * (q.w * q.x + q.y * q.z);
			var cosRCosP = +1.0 - 2.0 * (q.x * q.x + q.y * q.y);
			res.x = math.atan2(sinRCosP, cosRCosP);

			var sinP = +2.0 * (q.w * q.y - q.z * q.x);
			if (math.abs(sinP) >= 1) {
				res.y = math.PI / 2 * math.sign(sinP);
			} else {
				res.y = math.asin(sinP);
			}

			var sinYCosP = +2.0 * (q.w * q.z + q.x * q.y);
			var cosYCosP = +1.0 - 2.0 * (q.y * q.y + q.z * q.z);
			res.z = math.atan2(sinYCosP, cosYCosP);

			return (float3) res;
		}
	}
}
