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
					name: _data.Name,
					mesh: GetBaseMesh().Transform(preMatrix),
					material: table.GetMaterial(_data.Material),
					isVisible: _data.KickerType != KickerType.KickerInvisible
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
