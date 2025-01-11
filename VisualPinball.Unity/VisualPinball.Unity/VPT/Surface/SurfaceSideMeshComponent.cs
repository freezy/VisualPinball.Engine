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
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Mesh/Surface Side Mesh")]
	public class SurfaceSideMeshComponent : MeshComponent<SurfaceData, SurfaceComponent>
	{
		protected override Mesh GetMesh(SurfaceData data)
		{
			var playfieldComponent = GetComponentInParent<PlayfieldComponent>();
			return new SurfaceMeshGenerator(data)
				.GetMesh(SurfaceMeshGenerator.Side, playfieldComponent.Width, playfieldComponent.Height, 0, false)
				.TransformToWorld();
		}

		protected override PbrMaterial GetMaterial(SurfaceData data, Table table)
			=> new SurfaceMeshGenerator(data).GetMaterial(SurfaceMeshGenerator.Side, table, data);

		public override void RebuildMeshes()
		{
			base.RebuildMeshes();
			var mr = GetComponent<MeshRenderer>();
			if (mr) {
				mr.ResetBounds();
				mr.ResetLocalBounds();
			}
		}
	}
}
