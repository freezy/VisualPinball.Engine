using System;
using NLog;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Resources.Meshes;

namespace VisualPinball.Engine.VPT.Gate
{
	public class GateMeshGenerator : MeshGenerator
	{
		private readonly GateData _data;

		protected override Vertex3D Position => new Vertex3D(_data.Center.X, _data.Center.Y, _data.Height);
		protected override Vertex3D Scale => new Vertex3D(_data.Length, _data.Length, _data.Length);
		protected override float RotationZ => MathF.DegToRad(_data.Rotation);

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public GateMeshGenerator(GateData data)
		{
			_data = data;
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin, bool asRightHanded)
		{
			var (preMatrix, _) = GetPreMatrix(table, origin, asRightHanded);
			var postMatrix = GetPostMatrix(table, origin);
			return new RenderObjectGroup(_data.Name, "Gates", postMatrix, new RenderObject(
					name: "Wire",
					mesh: GetBaseMesh().Transform(preMatrix),
					material: table.GetMaterial(_data.Material),
					isVisible: _data.IsVisible
				),
				new RenderObject(
					name:"Bracket",
					mesh: GateBracketMesh.Clone().Transform(preMatrix),
					material: table.GetMaterial(_data.Material),
					isVisible: _data.IsVisible && _data.ShowBracket
				)
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
