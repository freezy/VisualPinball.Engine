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

		protected override Mesh GetMesh(LightData data)
		{
			var playfieldComponent = GetComponentInParent<PlayfieldComponent>();
			var meshGen = new SurfaceMeshGenerator(new LightInsertData(_dragPoints, InsertHeight));
			var topMesh = meshGen.GetMesh(SurfaceMeshGenerator.Top, playfieldComponent.Width, playfieldComponent.Height, 0, false);
			var sideMesh = meshGen.GetMesh(SurfaceMeshGenerator.Side, playfieldComponent.Width, playfieldComponent.Height, 0, false);
			return topMesh.Merge(sideMesh).TransformToWorld();
		}

		protected override PbrMaterial GetMaterial(LightData data, Table table)
		{
			var mat = table.GetMaterial(table.Data.PlayfieldMaterial);
			if (mat != null) {
				mat.Name += " (Playfield Insert)";
				return new PbrMaterial(mat, table.GetTexture(table.Data.Image));
			}

			mat = new Engine.VPT.Material("Playfield Insert");
			return new PbrMaterial(mat, table.GetTexture(table.Data.Image)) { DiffusionProfile = DiffusionProfileTemplate.Plastics };
		}
	}
}
