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
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity
{
	public static class MathExtensions
	{
		public static void Set(this ref float3 vector, float x, float y, float z)
		{
			vector.x = x;
			vector.y = y;
			vector.z = z;
		}

		public static bool IsZero(this ref float3 v)
		{
			return math.abs(v.x) < Constants.FloatMin && math.abs(v.y) < Constants.FloatMin &&
			       math.abs(v.z) < Constants.FloatMin;
		}

		public static void NormalizeSafe(this ref float3 v)
		{
			if (!v.IsZero()) {
				math.normalize(v);
			}
		}

		public static void RotationAroundAxis(this float3x3 m, float3 axis, float rSin, float rCos)
		{
			m.c0.x = axis.x * axis.x + rCos * (1.0f - axis.x * axis.x);
			m.c0.y = axis.x * axis.y * (1.0f - rCos) - axis.z * rSin;
			m.c0.z = axis.z * axis.x * (1.0f - rCos) + axis.y * rSin;

			m.c1.x = axis.x * axis.y * (1.0f - rCos) + axis.z * rSin;
			m.c1.y = axis.y * axis.y + rCos * (1.0f - axis.y * axis.y);
			m.c1.z = axis.y * axis.z * (1.0f - rCos) - axis.x * rSin;

			m.c2.x = axis.z * axis.x * (1.0f - rCos) - axis.y * rSin;
			m.c2.y = axis.y * axis.z * (1.0f - rCos) + axis.x * rSin;
			m.c2.z = axis.z * axis.z + rCos * (1.0f - axis.z * axis.z);
		}

		public static float3 GetScale(this float4x4 m)
		{
			return new float3(
				math.length(new float3(m.c0.x, m.c1.x, m.c2.x)),
				math.length(new float3(m.c0.y, m.c1.y, m.c2.y)),
				math.length(new float3(m.c0.z, m.c1.z, m.c2.z))
			);
		}

		public static bool IsPureTranslationMatrix(this float4x4 matrix)
		{
			// check scaling (diagonal elements)
			if (matrix.c0.x != 1.0f || matrix.c1.y != 1.0f || matrix.c2.z != 1.0f) {
				return false;
			}

			// Check rotation (non-diagonal elements)
			if (matrix.c0.y != 0.0f || matrix.c0.z != 0.0f || matrix.c1.x != 0.0f ||
			    matrix.c1.z != 0.0f || matrix.c2.x != 0.0f || matrix.c2.y != 0.0f) {
				return false;
			}

			// Check translation (last column)
			if (matrix.c3 is { x: 0.0f, y: 0.0f, z: 0.0f, w: 1.0f }) {
				return true;
			}

			return false;
		}

		public static Vertex3D ToVertex3D(this Vector3 vector)
		{
			return new Vertex3D(vector.x, vector.y, vector.z);
		}

		public static Vertex2D ToVertex2Dxy(this ref Vector3 vector)
		{
			return new Vertex2D(vector.x, vector.y);
		}

		public static Vector3 ToUnityVector3(this Vertex3D vertex)
		{
			return new Vector3(vertex.X, vertex.Y, vertex.Z);
		}

		public static Vector3 ToUnityVector2(this Vertex3D vertex)
		{
			return new Vector2(vertex.X, vertex.Y);
		}

		public static Vector3 ToUnityVector3(this RenderVertex3D vertex)
		{
			return new Vector3(vertex.X, vertex.Y, vertex.Z);
		}

		public static Vector3 ToUnityVector3(this ref Vertex3D vertex, float z)
		{
			return new Vector3(vertex.X, vertex.Y, z);
		}

		public static float3 ToUnityFloat3(this Vertex3D vertex)
		{
			return new float3(vertex.X, vertex.Y, vertex.Z);
		}

		public static float3 ToFloat3(this float2 vertex, float z)
		{
			return new Vector3(vertex.x, vertex.y, z);
		}


		public static float2 ToUnityFloat2(this RenderVertex2D vertex)
		{
			return new float2(vertex.X, vertex.Y);
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

		public static float2 ToUnityFloat2(this ref Vertex2D vertex)
		{
			return new float2(vertex.X, vertex.Y);
		}

		public static Vector3 ToUnityVector3(this ref Vertex3DNoTex2 vertex)
		{
			return new Vector3(vertex.X, vertex.Y, vertex.Z);
		}

		public static float3 ToUnityFloat3(this Vertex3DNoTex2 vertex)
		{
			return new float3(vertex.X, vertex.Y, vertex.Z);
		}

		public static Vector3 ToUnityNormalVector3(this ref Vertex3DNoTex2 vertex)
		{
			return new Vector3(vertex.Nx, vertex.Ny, vertex.Nz);
		}

		public static Vector3 ToUnityUvVector2(this ref Vertex3DNoTex2 vertex)
		{
			return new Vector2(vertex.Tu, -vertex.Tv);
		}

		internal static Aabb ToAabb(this Rect3D rect)
		{
			return new Aabb(rect.Left, rect.Right, rect.Top, rect.Bottom, rect.ZLow, rect.ZHigh);
		}

		public static float PercentageToRatio(this int percent)
		{
			return percent * 0.01f;
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
