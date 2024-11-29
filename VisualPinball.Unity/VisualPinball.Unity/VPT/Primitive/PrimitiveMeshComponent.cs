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

// ReSharper disable InconsistentNaming

using System;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Table;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Mesh/Primitive Mesh")]
	public class PrimitiveMeshComponent : MeshComponent<PrimitiveData, PrimitiveComponent>
	{
		#region Data

		[Tooltip("If true, don't use a real mesh but compute what Visual Pinball 8 used to use for primitives.")]
		public bool UseLegacyMesh;

		[Range(3, 32)]
		[Tooltip("How many sides to generate for the legacy mesh.")]
		public int Sides = 4;

		#endregion

		protected override bool IsProcedural => UseLegacyMesh;

		protected override Mesh GetMesh(PrimitiveData data)
			=> new PrimitiveMeshGenerator(data)
				.GetMesh(0, data.Mesh, Origin.Original, false)
				.TransformToWorld();

		protected override PbrMaterial GetMaterial(PrimitiveData data, Table table)
			=> new PrimitiveMeshGenerator(data).GetMaterial(table);
	}
}
