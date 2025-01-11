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
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Resources.Meshes;
using MathF = VisualPinball.Engine.Math.MathF;

namespace VisualPinball.Engine.VPT.HitTarget
{
	public class HitTargetMeshGenerator : MeshGenerator, IMeshGenerator
	{
		private readonly HitTargetData _data;
		private readonly Table.Table _table;

		protected override Vertex3D Position => _data.Position;
		protected override Vertex3D Scale => _data.Size;
		protected override float RotationZ => MathF.DegToRad(_data.RotZ);

		public string name => _data.Name;

		public HitTargetMeshGenerator(HitTargetData data, Table.Table table)
		{
			_data = data;
			_table = table;
		}

		public Mesh GetMesh() => GetBaseMesh();


		public Mesh GetMesh(Origin origin, bool asRightHanded)
		{
			var mesh = GetBaseMesh();
			var (preMatrix, _) = GetPreMatrix(BaseHeight(_table), origin, asRightHanded);
			return mesh.Transform(preMatrix);
		}

		public PbrMaterial GetMaterial()
		{
			return new PbrMaterial(_table.GetMaterial(_data.Material), _table.GetTexture(_data.Image));
		}

		protected override float BaseHeight(Table.Table table)
		{
			return table?.TableHeight ?? 0f;
		}

		private Mesh GetBaseMesh()
		{
			switch (_data.TargetType) {
				case TargetType.DropTargetBeveled: return DropTargetT2Mesh.Clone(_data.Name);
				case TargetType.DropTargetSimple: return DropTargetT3Mesh.Clone(_data.Name);
				case TargetType.DropTargetFlatSimple: return DropTargetT4Mesh.Clone(_data.Name);
				case TargetType.HitTargetRound: return HitTargetRoundMesh.Clone(_data.Name);
				case TargetType.HitTargetRectangle: return HitTargetRectangleMesh.Clone(_data.Name);
				case TargetType.HitFatTargetRectangle: return HitTargetFatRectangleMesh.Clone(_data.Name);
				case TargetType.HitFatTargetSquare: return HitTargetFatSquareMesh.Clone(_data.Name);
				case TargetType.HitTargetSlim: return HitTargetT1SlimMesh.Clone(_data.Name);
				case TargetType.HitFatTargetSlim: return HitTargetT2SlimMesh.Clone(_data.Name);
				default: return DropTargetT3Mesh.Clone(_data.Name);
			}
		}

		#region Mesh Imports

		private static readonly Mesh DropTargetT2Mesh = new Mesh(DropTargetT2.Vertices, DropTargetT2.Indices);
		private static readonly Mesh DropTargetT3Mesh = new Mesh(DropTargetT3.Vertices, DropTargetT3.Indices);
		private static readonly Mesh DropTargetT4Mesh = new Mesh(DropTargetT4.Vertices, DropTargetT4.Indices);
		private static readonly Mesh HitTargetRoundMesh = new Mesh(HitTargetRound.Vertices, HitTargetRound.Indices);
		private static readonly Mesh HitTargetRectangleMesh = new Mesh(HitTargetRectangle.Vertices, HitTargetRectangle.Indices);
		private static readonly Mesh HitTargetFatRectangleMesh = new Mesh(HitTargetFatRectangle.Vertices, HitTargetFatRectangle.Indices);
		private static readonly Mesh HitTargetFatSquareMesh = new Mesh(HitTargetFatSquare.Vertices, HitTargetFatSquare.Indices);
		private static readonly Mesh HitTargetT1SlimMesh = new Mesh(HitTargetT1Slim.Vertices, HitTargetT1Slim.Indices);
		private static readonly Mesh HitTargetT2SlimMesh = new Mesh(HitTargetT2Slim.Vertices, HitTargetT2Slim.Indices);


		#endregion
	}
}
