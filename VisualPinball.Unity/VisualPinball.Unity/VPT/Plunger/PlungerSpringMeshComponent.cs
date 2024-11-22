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

using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Table;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Mesh/Plunger Spring Mesh")]
	public class PlungerSpringMeshComponent : PlungerMeshComponent
	{
		#region Data

		public float SpringDiam = 0.77f;

		public float SpringGauge = 1.38f;

		public float SpringLoops = 8.0f;

		public float SpringEndLoops = 2.5f;

		#endregion

		protected override Mesh GetMesh(PlungerData data)
			=> new PlungerMeshGenerator(data).GetMesh(MainComponent.PositionZ, PlungerMeshGenerator.Spring);

		protected override PbrMaterial GetMaterial(PlungerData data, Table table)
			=> new PlungerMeshGenerator(data).GetMaterial(table);

		public override void RebuildMeshes()
		{
			base.RebuildMeshes();
			CalculateBoundingBox();
		}

		public void CalculateBoundingBox()
		{
			var plungerComp = GetComponentInParent<PlungerComponent>();
			var rodComp = plungerComp.GetComponentInChildren<PlungerRodMeshComponent>();
			var smr = GetComponent<SkinnedMeshRenderer>();
			var bounds = smr.localBounds;
			var ringOffset = rodComp != null ? rodComp.RingGap + rodComp.RingWidth : 0f;
			var radius = plungerComp.Width / 2 * SpringDiam + 2f;
			bounds.center = new Vector3(0, 25f, -(ringOffset - 25)) * Physics.ScaleInv;
			bounds.extents = new Vector3(radius, radius, -110f) * Physics.ScaleInv;
			smr.localBounds = bounds;
		}
	}
}
