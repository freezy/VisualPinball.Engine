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

		protected override Tuple<Matrix3D, Matrix3D> GetTransformationMatrix(Table.Table table)
		{
			var baseHeight = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y) * table.GetScaleZ();

			// scale matrix
			var scaleMatrix = new Matrix3D();
			scaleMatrix.SetScaling(_data.Length, _data.Length, _data.Length);

			// translation matrix
			var transMatrix = new Matrix3D();
			transMatrix.SetTranslation(_data.Center.X, _data.Center.Y, _data.Height + baseHeight);

			// rotation matrix
			var rotMatrix = new Matrix3D();
			rotMatrix.RotateZMatrix(MathF.DegToRad(_data.Rotation));

			var fullMatrix = scaleMatrix.Clone();
			fullMatrix.Multiply(rotMatrix);
			fullMatrix.Multiply(transMatrix);
			scaleMatrix.SetScaling(1.0f, 1.0f, table.GetScaleZ());
			fullMatrix.Multiply(scaleMatrix);

			return new Tuple<Matrix3D, Matrix3D>(fullMatrix, null);
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
