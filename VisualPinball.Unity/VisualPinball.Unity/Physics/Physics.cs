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
using VisualPinball.Engine.Math;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	
	/// <summary>
	/// This static class is a collection of transformation methods to convert from VPX space to world space
	/// and vice versa.
	/// </summary>
	public static class Physics
	{
		#region Definitions

		private const float Scale = 1852.71f;
		private const float ScaleInv = (float)(1 / (double)Scale);

		private static readonly float2 Translate = new(0, 0);

		public static readonly float4x4 WorldToVpx = new(
			new float4(Scale, 0, 0, 0),
			new float4(0, 0, Scale, 0),
			new float4(0, -Scale, 0, 0),
			new float4(Translate.x, Translate.y, 0, 1f)
		);
		
		public static readonly float4x4 VpxToWorld = new(
			new float4(ScaleInv, 0, 0, 0),
			new float4(0, 0, -ScaleInv, 0),
			new float4(0, ScaleInv, 0, 0),
			new float4(-(float)(Translate.x / (double)Scale), 0, (float)(Translate.y / (double)Scale), 1f)
		);

		#endregion

		#region Transformation

		public static Matrix3D TransformToVpx(this Matrix3D vpx) => WorldToVpx.ToVpMatrix().Multiply(vpx);
		public static Mesh TransformToWorld(this Mesh mesh) => mesh?.Transform(VpxToWorld.ToVpMatrix());
		public static Mesh TransformToVpx(this Mesh mesh) => mesh?.Transform(WorldToVpx.ToVpMatrix());

		
		/// <summary>
		/// Use this on matrices that are generated for VPX-space transformations that you want to apply to a mesh that
		/// has already been transformed to world-space.
		/// </summary>
		/// <param name="m">VPX-space matrix that is supposed to be applied to a VPX-space mesh</param>
		/// <returns>Matrix that with the same transformation to be applied to a mesh converted to world-space.</returns>
		public static Matrix4x4 TransformVpxInWorld(this Matrix4x4 m) => math.mul(math.mul(VpxToWorld, m), WorldToVpx);

		//public static float3 MultiplyPoint(this float4x4 matrix, float3 p) => math.mul(matrix, new float4(p, 1f)).xyz;
		public static float3 MultiplyPoint(this float4x4 matrix, float3 p) => math.transform(matrix, p);

		/// <summary>
		/// Returns the transformation matrix of an item in VPX space.<br/>
		///
		/// You basically give the world-to-local transformation matrix of the item, and you'll get the
		/// transformation in VPX space (i.e. relative to the playfield, however it's transformed).
		/// </summary>
		/// <param name="worldToLocal">World-to-local transformation matrix of the item.</param>
		/// <param name="playfieldToWorld">Local-to-World transformation matrix of the playfield.</param>
		/// <returns>Transformation matrix of the item in VPX space.</returns>
		public static float4x4 WorldToLocalTranslateWithinPlayfield(this Matrix4x4 worldToLocal, float4x4 playfieldToWorld) => math.mul(
			math.mul(Physics.WorldToVpx,
				math.inverse(math.mul(worldToLocal, playfieldToWorld))
			),
			Physics.VpxToWorld);

		#endregion

		#region Translation

		public static float3 TranslateToVpx(this float3 worldVector) => math.transform(WorldToVpx, worldVector);
		public static Vector3 TranslateToVpx(this Vector3 worldVector) => math.transform(WorldToVpx, worldVector);
		
		public static float3 TranslateToWorld(this float3 vpxVector) => math.transform(VpxToWorld, vpxVector);
		public static Vector3 TranslateToWorld(this Vector3 vpxVector) => math.transform(VpxToWorld, vpxVector);
		public static Vector3 TranslateToWorld(float vpxX, float vpxY, float vpxZ) => TranslateToWorld(new float3(vpxX, vpxY, vpxZ));

		#endregion

		#region Scale

		public static Vector3 ScaleInvVector = new(ScaleInv, ScaleInv, ScaleInv);

		public static float ScaleToVpx(float worldSize) => worldSize * Scale;
		public static float ScaleToWorld(float vpxSize) => vpxSize * ScaleInv;
		
		public static float3 ScaleToWorld(float vpxX, float vpxY, float vpxZ) => new(ScaleToWorld(vpxX), ScaleToWorld(vpxY), ScaleToWorld(vpxZ));
		public static float3 ScaleToWorld(float3 vpxSize) => new(ScaleToWorld(vpxSize.x), ScaleToWorld(vpxSize.y), ScaleToWorld(vpxSize.z));

		#endregion
		
		#region Rotation

		private static readonly Quaternion ToWorldRotation = ((Matrix4x4)VpxToWorld).rotation;
		private static readonly Quaternion ToVpxRotation = ((Matrix4x4)WorldToVpx).rotation;

		public static Quaternion RotateToWorld(this Quaternion q) => Quaternion.Euler(RotateToWorld(q.eulerAngles));
		public static float3 RotateToWorld(float vpxX, float vpxY, float vpxZ) => ((Matrix4x4)math.mul(VpxToWorld, float4x4.Euler(math.radians(vpxX), math.radians(vpxY), math.radians(vpxZ)))).rotation.eulerAngles;
		private static float3 RotateToWorld(float3 vpxRotation) => ((Matrix4x4)math.mul(VpxToWorld, float4x4.Euler(math.radians(vpxRotation.x), math.radians(vpxRotation.y), math.radians(vpxRotation.z)))).rotation.eulerAngles;

		#endregion
	}
}
