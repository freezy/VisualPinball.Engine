// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

namespace VisualPinball.Unity
{
	public static class Physics
	{
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

		public static Vector3 ScaleInvVector = new(ScaleInv, ScaleInv, ScaleInv);
		
		public static float4x4 TransformToVpx(Matrix4x4 vpx) => math.mul(WorldToVpx, vpx);
		public static Matrix3D TransformToVpx(this Matrix3D vpx) => WorldToVpx.ToVpMatrix().Multiply(vpx);
		public static float4x4 TransformToWorld(Matrix4x4 world) => math.mul(VpxToWorld, world);
		public static VisualPinball.Engine.VPT.Mesh TransformToWorld(this VisualPinball.Engine.VPT.Mesh mesh) => mesh.Transform(VpxToWorld.ToVpMatrix());
		public static void TransformToWorld(this Transform transform)
		{
			transform.localPosition = transform.localPosition.TranslateToWorld();
			transform.localScale = ScaleToWorld(transform.localScale);
			transform.localRotation = Quaternion.Euler(RotateToWorld(transform.localRotation.eulerAngles));
		}

		public static float3 TranslateToVpx(float3 worldVector) => math.transform(WorldToVpx, worldVector);
		public static float3 TranslateToVpx(this Vector3 worldVector) => math.transform(WorldToVpx, worldVector);
		public static float3 TranslateToVpx(float worldX, float worldY, float worldZ) => TranslateToVpx(new float3(worldX, worldY, worldZ));
		public static float3 TranslateToWorld(this float3 vpxVector) => math.transform(VpxToWorld, vpxVector);
		public static float3 TranslateToWorld(this Vector3 vpxVector) => math.transform(VpxToWorld, vpxVector);
		public static Vector3 TranslateToWorld(float vpxX, float vpxY, float vpxZ) => TranslateToWorld(new float3(vpxX, vpxY, vpxZ));
		public static float ScaleToVpx(float worldSize) => worldSize * Scale;
		public static float ScaleToWorld(float vpxSize) => vpxSize * ScaleInv;
		
		public static float3 ScaleToVpx(float worldX, float worldY, float worldZ) => new(ScaleToVpx(worldX), ScaleToVpx(worldY), ScaleToVpx(worldZ));
		public static float3 ScaleToVpx(float3 worldSize) => new(ScaleToVpx(worldSize.x), ScaleToVpx(worldSize.y), ScaleToVpx(worldSize.z));
		public static float3 ScaleToWorld(float vpxX, float vpxY, float vpxZ) => new(ScaleToWorld(vpxX), ScaleToWorld(vpxY), ScaleToWorld(vpxZ));
		public static float3 ScaleToWorld(float3 vpxSize) => new(ScaleToWorld(vpxSize.x), ScaleToWorld(vpxSize.y), ScaleToWorld(vpxSize.z));

		public static float3 RotateToVpx(float worldX, float worldY, float worldZ) => ((Matrix4x4)math.mul(WorldToVpx, float4x4.Euler(math.radians(worldX), math.radians(worldY), math.radians(worldZ)))).rotation.eulerAngles;
		public static float3 RotateToVpx(float3 worldRotation) => ((Matrix4x4)math.mul(WorldToVpx, float4x4.Euler(math.radians(worldRotation.x), math.radians(worldRotation.y), math.radians(worldRotation.z)))).rotation.eulerAngles;
		public static float3 RotateToWorld(float vpxX, float vpxY, float vpxZ) => ((Matrix4x4)math.mul(VpxToWorld, float4x4.Euler(math.radians(vpxX), math.radians(vpxY), math.radians(vpxZ)))).rotation.eulerAngles;
		public static float3 RotateToWorld(float3 vpxRotation) => ((Matrix4x4)math.mul(VpxToWorld, float4x4.Euler(math.radians(vpxRotation.x), math.radians(vpxRotation.y), math.radians(vpxRotation.z)))).rotation.eulerAngles;

		/// <summary>
		/// Use this on matrices that are generated for VPX-space transformations that you want to apply to a mesh that
		/// has already been transformed to world-space. 
		/// </summary>
		/// <param name="m">VPX-space matrix that is supposed to be applied to a VPX-space mesh</param>
		/// <returns>Matrix that with the same transformation to be applied to a mesh converted to world-space.</returns>
		public static Matrix4x4 ApplyVpxMatrix(this Matrix4x4 m) => math.mul(math.mul(VpxToWorld, m), WorldToVpx);
	}
}
