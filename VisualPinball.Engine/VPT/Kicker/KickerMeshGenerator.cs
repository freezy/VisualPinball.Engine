// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Resources.Meshes;

namespace VisualPinball.Engine.VPT.Kicker
{
	public class KickerMeshGenerator : MeshGenerator
	{
		private readonly KickerData _data;

		protected override Vertex3D Position => new Vertex3D(
			_data.Center.X,
			_data.Center.Y,
			(_data.KickerType == KickerType.KickerCup ? -0.18f : 0f) * _data.Radius
		);

		protected override Vertex3D Scale => new Vertex3D(_data.Radius, _data.Radius, _data.Radius);

		protected override float RotationZ { get {
			switch (_data.KickerType) {
				case KickerType.KickerCup: return MathF.DegToRad(_data.Orientation);
				case KickerType.KickerWilliams: return MathF.DegToRad(_data.Orientation + 90.0f);
				default: return MathF.DegToRad(0.0f);
			}
		} }

		public KickerMeshGenerator(KickerData data)
		{
			_data = data;
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin, bool asRightHanded)
		{
			var (preMatrix, _) = GetPreMatrix(table, origin, asRightHanded);
			var postMatrix = GetPostMatrix(table, origin);
			return new RenderObjectGroup(_data.Name, "Kickers", postMatrix, new RenderObject(
					_data.Name,
					GetBaseMesh().Transform(preMatrix),
					new PbrMaterial(table.GetMaterial(_data.Material)),
					_data.KickerType != KickerType.KickerInvisible
				)
			);
		}

		protected override float BaseHeight(Table.Table table)
		{
			return table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
		}

		private Mesh GetBaseMesh()
		{
			switch (_data.KickerType) {
				case KickerType.KickerCup: return KickerCupMesh.Clone(_data.Name);
				case KickerType.KickerWilliams: return KickerWilliamsMesh.Clone(_data.Name);
				case KickerType.KickerGottlieb: return KickerGottliebMesh.Clone(_data.Name);
				case KickerType.KickerCup2: return KickerT1Mesh.Clone(_data.Name);
				case KickerType.KickerHole: return KickerHoleMesh.Clone(_data.Name);
				case KickerType.KickerHoleSimple: return KickerSimpleHoleMesh.Clone(_data.Name);
				default:  return KickerSimpleHoleMesh.Clone(_data.Name);
			}
		}

		#region Mesh Imports

		private static readonly Mesh KickerCupMesh = new Mesh(KickerCup.Vertices, KickerCup.Indices);
		private static readonly Mesh KickerGottliebMesh = new Mesh(KickerGottlieb.Vertices, KickerGottlieb.Indices);
		private static readonly Mesh KickerHoleMesh = new Mesh(KickerHole.Vertices, KickerHole.Indices);
		private static readonly Mesh KickerSimpleHoleMesh = new Mesh(KickerSimpleHole.Vertices, KickerSimpleHole.Indices);
		private static readonly Mesh KickerT1Mesh = new Mesh(KickerT1.Vertices, KickerT1.Indices);
		private static readonly Mesh KickerWilliamsMesh = new Mesh(KickerWilliams.Vertices, KickerWilliams.Indices);

		#endregion
	}
}
