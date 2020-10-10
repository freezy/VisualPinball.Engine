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
using NLog;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Resources.Meshes;

namespace VisualPinball.Engine.VPT.Gate
{
	public class GateMeshGenerator : MeshGenerator
	{
		public const string Wire = "Wire";
		public const string Bracket = "Bracket";

		private readonly GateData _data;

		protected override Vertex3D Position => new Vertex3D(_data.Center.X, _data.Center.Y, _data.Height);
		protected override Vertex3D Scale => new Vertex3D(_data.Length, _data.Length, _data.Length);
		protected override float RotationZ => MathF.DegToRad(_data.Rotation);

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public GateMeshGenerator(GateData data)
		{
			_data = data;
		}

		public RenderObject GetRenderObject(Table.Table table, string id, Origin origin, bool asRightHanded)
		{
			var (preMatrix, _) = GetPreMatrix(table, origin, asRightHanded);
			switch (id) {
				case Wire:
					return new RenderObject(
						id,
						GetBaseMesh().Transform(preMatrix),
						new PbrMaterial(table.GetMaterial(_data.Material)),
						_data.IsVisible
					);
				case Bracket:
					return new RenderObject(
						id,
						GateBracketMesh.Clone().Transform(preMatrix),
						new PbrMaterial(table.GetMaterial(_data.Material)),
						_data.IsVisible && _data.ShowBracket
					);
				default:
					throw new ArgumentException("Unknown gate mesh \"" + id + "\".");
			}
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin, bool asRightHanded)
		{
			var postMatrix = GetPostMatrix(table, origin);
			return new RenderObjectGroup(_data.Name, "Gates", postMatrix,
				GetRenderObject(table, Wire, origin, asRightHanded),
				GetRenderObject(table, Bracket, origin, asRightHanded)
			);
		}

		protected override float BaseHeight(Table.Table table)
		{
			return table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
		}

		private Mesh GetBaseMesh()
		{
			switch (_data.GateType) {
				case GateType.GateWireW: return GateWireMesh.Clone();
				case GateType.GateWireRectangle: return GateWireRectangleMesh.Clone();
				case GateType.GatePlate: return GatePlateMesh.Clone();
				case GateType.GateLongPlate: return GateLongPlateMesh.Clone();
				default:
					Logger.Warn($"[GateMeshGenerator.GetBaseMesh] Unknown gate type \"{_data.GateType}\"");
					return GateWireMesh.Clone();
			}
		}

		#region Mesh Imports

		private static readonly Mesh GateWireMesh = new Mesh("Wire", GateWire.Vertices, GateWire.Indices);
		private static readonly Mesh GateWireRectangleMesh = new Mesh("Wire", GateWireRectangle.Vertices, GateWireRectangle.Indices);
		private static readonly Mesh GatePlateMesh = new Mesh("Wire", GatePlate.Vertices, GatePlate.Indices);
		private static readonly Mesh GateLongPlateMesh = new Mesh("Wire", GateLongPlate.Vertices, GateLongPlate.Indices);
		private static readonly Mesh GateBracketMesh = new Mesh("Bracket", GateBracket.Vertices, GateBracket.Indices);

		#endregion

	}
}
