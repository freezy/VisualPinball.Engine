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

		public static float4x4 TransformToVpx(Matrix4x4 vpx) => math.mul(WorldToVpx, vpx);
		public static float4x4 TransformToWorld(Matrix4x4 world) => math.mul(VpxToWorld, world);

		public static float3 TranslateToVpx(float3 worldVector) => math.transform(WorldToVpx, worldVector);
		public static float3 TranslateToVpx(float worldX, float worldY, float worldZ) => TranslateToVpx(new float3(worldX, worldY, worldZ));
		public static float3 TranslateToWorld(float3 vpxVector) => math.transform(VpxToWorld, vpxVector);
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

		public static VisualPinball.Engine.VPT.Mesh TransformToWorld(this VisualPinball.Engine.VPT.Mesh mesh) => mesh.Transform(VpxToWorld.ToVpMatrix());
	}
}
