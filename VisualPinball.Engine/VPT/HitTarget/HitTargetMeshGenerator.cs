using System;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Resources.Meshes;

namespace VisualPinball.Engine.VPT.HitTarget
{
	public class HitTargetMeshGenerator : MeshGenerator
	{
		private readonly HitTargetData _data;

		public HitTargetMeshGenerator(HitTargetData data)
		{
			_data = data;
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin, bool asRightHanded)
		{
			var mesh = GetBaseMesh();
			var (preMatrix, _) = GetPreMatrix(table, origin, asRightHanded);
			var postMatrix = GetPostMatrix(table, origin);
			return new RenderObjectGroup(_data.Name, "HitTargets", new RenderObject(
				name: _data.Name,
				mesh: mesh.Transform(preMatrix),
				material: table.GetMaterial(_data.Material),
				map: table.GetTexture(_data.Image),
				isVisible: _data.IsVisible
			), postMatrix);
		}

		protected override Tuple<Matrix3D, Matrix3D> GetTransformationMatrix(Table.Table table)
		{
			var dropOffset = 0f;
			if (_data.IsDropTarget && _data.IsDropped) {
				dropOffset = -(HitTarget.DropTargetLimit * table.GetScaleZ());
			}

			// scale matrix
			var scaleMatrix = new Matrix3D();
			scaleMatrix.SetScaling(_data.Size.X, _data.Size.Y, _data.Size.Z);

			// translation matrix
			var transMatrix = new Matrix3D();
			transMatrix.SetTranslation(_data.Position.X, _data.Position.Y, _data.Position.Z + dropOffset);

			// // rotation matrix
			var rotMatrix = new Matrix3D();
			rotMatrix.RotateZMatrix(MathF.DegToRad(_data.RotZ));

			var fullMatrix = scaleMatrix.Clone();
			fullMatrix.Multiply(rotMatrix);
			fullMatrix.Multiply(transMatrix);  // fullMatrix = Smatrix * RTmatrix * Tmatrix
			scaleMatrix.SetScaling(1.0f, 1.0f, table.GetScaleZ());
			fullMatrix.Multiply(scaleMatrix);

			return new Tuple<Matrix3D, Matrix3D>(fullMatrix, null);
		}

		// private Mesh GenerateMesh()
		// {
		// 	var hitTargetMesh = GetBaseMesh();
		//
		// 	var fullMatrix = new Matrix3D();
		// 	var tempMatrix = new Matrix3D();
		// 	tempMatrix.RotateZMatrix(MathF.DegToRad(_data.RotZ));
		// 	fullMatrix.Multiply(tempMatrix);
		//
		// 	foreach (var vertex in hitTargetMesh.Vertices) {
		// 		var vert = vertex.GetVertex();
		// 		// vert.X *= _data.Size.X;
		// 		// vert.Y *= _data.Size.Y;
		// 		// vert.Z *= _data.Size.Z;
		// 		//vert.MultiplyMatrix(fullMatrix);
		//
		// 		vertex.X = vert.X; // + _data.Position.X;
		// 		vertex.Y = vert.Y; // + _data.Position.Y;
		// 		vertex.Z = vert.Z; // * table.GetScaleZ() + _data.Position.Z + table.GetTableHeight() + dropOffset;
		//
		// 		var normal = vertex.GetNormal().MultiplyMatrixNoTranslate(fullMatrix);
		// 		vertex.Nx = normal.X;
		// 		vertex.Ny = normal.Y;
		// 		vertex.Nz = normal.Z;
		// 	}
		// 	return hitTargetMesh;
		// }

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
