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
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Table;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Mesh/Plunger Flat Mesh")]
	public class PlungerFlatMeshComponent : PlungerMeshComponent
	{
		protected override Mesh GetMesh(PlungerData data)
			=> new PlungerMeshGenerator(data)
				.GetMesh(MainComponent.Position.z, PlungerMeshGenerator.Flat)
				.TransformToWorld();

		protected override PbrMaterial GetMaterial(PlungerData data, Table table)
			=> new PlungerMeshGenerator(data).GetMaterial(table);
	}
}
