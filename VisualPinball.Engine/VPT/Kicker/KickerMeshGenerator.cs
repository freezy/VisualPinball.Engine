using VisualPinball.Engine.Game;
using VisualPinball.Engine.Resources.Meshes;

namespace VisualPinball.Engine.VPT.Kicker
{
	public class KickerMeshGenerator
	{
		private readonly KickerData _data;

		public KickerMeshGenerator(KickerData data)
		{
			_data = data;
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin, bool asRightHanded)
		{

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
