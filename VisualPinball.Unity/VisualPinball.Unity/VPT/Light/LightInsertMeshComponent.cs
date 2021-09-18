// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Light;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	public class LightInsertMeshComponent : MeshComponent<LightData, LightComponent>
	{
		#region Data

		public float InsertHeight = 20f;

		public float PositionZ = 0.1f;

		[SerializeField]
		private DragPointData[] _dragPoints;
		public DragPointData[] DragPoints { get => _dragPoints; set => _dragPoints = value; }

		#endregion

		public bool DragPointsActive => true;

		public const string InsertObjectName = "Insert";

		public override IEnumerable<Type> ValidParents => Type.EmptyTypes;

		protected override RenderObject GetRenderObject(LightData data, Table table)
		{
			return new RenderObject(
				InsertObjectName,
				GetMesh(data),
				new PbrMaterial(table.GetMaterial(table.Data.PlayfieldMaterial), table.GetTexture(table.Data.Image)),
				false
			);
		}

		protected override Mesh GetMesh(LightData data)
		{
			var playfieldComponent = GetComponentInParent<PlayfieldComponent>();
			var meshGen = new SurfaceMeshGenerator(new LightInsertData(_dragPoints, InsertHeight));
			var topMesh = meshGen.GetMesh(SurfaceMeshGenerator.Top, playfieldComponent.Width, playfieldComponent.Height, playfieldComponent.PlayfieldHeight);
			var sideMesh = meshGen.GetMesh(SurfaceMeshGenerator.Side, playfieldComponent.Width, playfieldComponent.Height, playfieldComponent.PlayfieldHeight);
			return topMesh.Merge(sideMesh);
		}
	}
}
