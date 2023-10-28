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

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity
{

	internal unsafe struct ColliderMeshData : IDisposable
	{
		[NativeDisableUnsafePtrRestriction] private void* _vertices;
		[NativeDisableUnsafePtrRestriction] private void* _normals;

		private readonly int _length;

		private readonly Allocator _allocator;

		public ColliderMeshData(IList<Vertex3DNoTex2> vertices, float radius, float3 position, Allocator allocator)
		{
			var rad = radius * 0.8f;
			_length = UnsafeUtility.SizeOf<float3>() * vertices.Count;
			_vertices = UnsafeUtility.Malloc(_length, UnsafeUtility.AlignOf<float3>(), allocator);
			_normals = UnsafeUtility.Malloc(_length, UnsafeUtility.AlignOf<float3>(), allocator);

			for (var i = 0; i < vertices.Count; i++) {
				UnsafeUtility.WriteArrayElement(_vertices, i, new float3(
					vertices[i].X * rad + position.x,
					vertices[i].Y * rad + position.y,
					vertices[i].Z * rad + position.z
				));
				UnsafeUtility.WriteArrayElement(_normals, i, new float3(
					vertices[i].Nx,
					vertices[i].Ny,
					vertices[i].Nz
				));
			}
			_allocator = allocator;
		}

		public UnmanagedArray<float3> Vertices => new(_vertices, _length);
		public UnmanagedArray<float3> Normals => new(_normals, _length);

		public void Dispose()
		{
			UnsafeUtility.Free(_vertices, _allocator);
			UnsafeUtility.Free(_normals, _allocator);

			_vertices = null;
			_normals = null;
		}
	}
}
