﻿// Visual Pinball Engine
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
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Table;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	[PackAs("FlipperRubberMesh")]
	[ExecuteInEditMode]
	[AddComponentMenu("Pinball/Mesh/Flipper Rubber Mesh")]
	public class FlipperRubberMeshComponent : MeshComponent<FlipperData, FlipperComponent>, IPackable
	{
		protected override PbrMaterial GetMaterial(FlipperData data, Table table)
			=> FlipperMeshGenerator.GetMaterial(FlipperMeshGenerator.Rubber, table, data);

		protected override Mesh GetMesh(FlipperData _)
			=> new FlipperMeshGenerator(MainComponent)
				.GetMesh(FlipperMeshGenerator.Rubber, 0)
				.TransformToWorld();

		#region Packaging

		public byte[] Pack() => PackageApi.Packer.Empty;

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => null;

		public void Unpack(byte[] bytes) { }

		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) { }

		#endregion
	}
}
