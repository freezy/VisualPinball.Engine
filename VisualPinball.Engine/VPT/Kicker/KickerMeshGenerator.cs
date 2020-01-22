using System;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Resources.Meshes;

namespace VisualPinball.Engine.VPT.Kicker
{
	public class KickerMeshGenerator : MeshGenerator
	{
		private readonly KickerData _data;

		public KickerMeshGenerator(KickerData data)
		{
			_data = data;
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin, bool asRightHanded)
		{
			var (preMatrix, _) = GetPreMatrix(table, origin, asRightHanded);
			var postMatrix = GetPostMatrix(table, origin);
			return new RenderObjectGroup(_data.Name, "Kickers", postMatrix, new RenderObject(
					name: _data.Name,
					mesh: GetBaseMesh().Transform(preMatrix),
					material: table.GetMaterial(_data.Material),
					isVisible: _data.KickerType != KickerType.KickerInvisible
				)
			);
		}

		protected override Tuple<Matrix3D, Matrix3D> GetTransformationMatrix(Table.Table table)
		{
			var zOffset = 0.0f;
			var zRot = _data.Orientation;
			switch (_data.KickerType) {
				case KickerType.KickerCup:
					zOffset = -0.18f;
					break;
				case KickerType.KickerWilliams:
					zRot = _data.Orientation + 90.0f;
					break;
				case KickerType.KickerHole:
					zRot = 0.0f;
					break;
				default:
					zRot = 0.0f;
					break;
			}

			// scale matrix
			var scaleMatrix = new Matrix3D();
			scaleMatrix.SetScaling(_data.Radius, _data.Radius, _data.Radius);

			// translation matrix
			var transMatrix = new Matrix3D();
			transMatrix.SetTranslation(_data.Center.X, _data.Center.Y, zOffset);

			// rotation matrix
			var rotMatrix = new Matrix3D();
			rotMatrix.RotateZMatrix(MathF.DegToRad(zRot));

			var fullMatrix = scaleMatrix.Clone();
			fullMatrix.Multiply(rotMatrix);
			fullMatrix.Multiply(transMatrix);
			scaleMatrix.SetScaling(1.0f, 1.0f, table.GetScaleZ());
			fullMatrix.Multiply(scaleMatrix);

			return new Tuple<Matrix3D, Matrix3D>(fullMatrix, null);
		}

		private Mesh GetBaseMesh()
		{
			switch (_data.KickerType) {
				case KickerType.KickerCup: return KickerCupMesh.Clone();
				case KickerType.KickerWilliams: return KickerWilliamsMesh.Clone();
				case KickerType.KickerGottlieb: return KickerGottliebMesh.Clone();
				case KickerType.KickerCup2: return KickerT1Mesh.Clone();
				case KickerType.KickerHole: return KickerHoleMesh.Clone();
				case KickerType.KickerHoleSimple: return KickerSimpleHoleMesh.Clone();
				default:  return KickerSimpleHoleMesh.Clone();
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
