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

using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.MetalWireGuide;
using VisualPinball.Engine.VPT.Table;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Mesh/Metal Wire Guide Mesh")]
	public class MetalWireGuideMeshComponent : MeshComponent<MetalWireGuideData, MetalWireGuideComponent>
	{
		protected override Mesh GetMesh(MetalWireGuideData data)
			=> new MetalWireGuideMeshGenerator(MainComponent)
				.GetTransformedMesh(0, MainComponent.Height, MainComponent.PlayfieldDetailLevel, MainComponent.Bendradius)
				.TransformToWorld();

		protected override PbrMaterial GetMaterial(MetalWireGuideData data, Table table)
			=> new MetalWireGuideMeshGenerator(MainComponent).GetMaterial(table, data);

		public override void RebuildMeshes()
		{
			base.RebuildMeshes();
			var mr = GetComponent<MeshRenderer>();
			mr.ResetBounds();
			mr.ResetLocalBounds();
		}
	}
}
