using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity
{
	public static class MathExtensions
	{
		public static Vertex3D ToVertex3D(this Vector3 vector)
		{
			return new Vertex3D(vector.x, vector.y, vector.z);
		}

		public static Vertex2D ToVertex2Dxy(this Vector3 vector)
		{
			return new Vertex2D(vector.x, vector.y);
		}

		public static Vector3 ToUnityVector3(this Vertex3D vertex)
		{
			return new Vector3(vertex.X, vertex.Y, vertex.Z);
		}

		public static float3 ToUnityFloat3(this Vertex3D vertex)
		{
			return new float3(vertex.X, vertex.Y, vertex.Z);
		}

		public static Vertex3D ToVertex3D(this Vector2 vector, float z)
		{
			return new Vertex3D(vector.x, vector.y, z);
		}

		public static Vertex2D ToVertex2D(this Vector2 vector)
		{
			return new Vertex2D(vector.x, vector.y);
		}

		public static Vector3 ToUnityVector3(this Vertex2D vertex, float z)
		{
			return new Vector3(vertex.X, vertex.Y, z);
		}

		public static Vector2 ToUnityVector2(this Vertex2D vertex)
		{
			return new Vector2(vertex.X, vertex.Y);
		}

		public static float2 ToUnityFloat2(this Vertex2D vertex)
		{
			return new float2(vertex.X, vertex.Y);
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

		public static Aabb ToAabb(this Rect3D rect, int colliderId)
		{
			return new Aabb(colliderId, rect.Left, rect.Right, rect.Top, rect.Bottom, rect.ZLow, rect.ZHigh);
		}

		public static void ToAabb(this Rect3D rect, ref Aabb aabb, int colliderId)
		{
			aabb.ColliderId = colliderId;
			aabb.Left = rect.Left;
			aabb.Right = rect.Right;
			aabb.Top = rect.Top;
			aabb.Bottom = rect.Bottom;
			aabb.ZLow = rect.ZLow;
			aabb.ZHigh = rect.ZHigh;
		}

		public static float3x3 ToUnityFloat3x3(this Matrix2D matrix)
		{
			return new float3x3(
				matrix.Matrix[0][0],
				matrix.Matrix[0][1],
				matrix.Matrix[0][2],
				matrix.Matrix[1][0],
				matrix.Matrix[1][1],
				matrix.Matrix[1][2],
				matrix.Matrix[2][0],
				matrix.Matrix[2][1],
				matrix.Matrix[2][2]
			);
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
