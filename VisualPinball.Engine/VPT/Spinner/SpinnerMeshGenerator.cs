using System;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Resources.Meshes;

namespace VisualPinball.Engine.VPT.Spinner
{
	public class SpinnerMeshGenerator : MeshGenerator
	{
		private readonly SpinnerData _data;

		protected override Vertex3D Position => new Vertex3D(_data.Center.X, _data.Center.Y, _data.Height);
		protected override Vertex3D Scale => new Vertex3D(_data.Length, _data.Length, _data.Length);
		protected override float RotationZ => MathF.DegToRad(_data.Rotation);

		public SpinnerMeshGenerator(SpinnerData data)
		{
			_data = data;
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin, bool asRightHanded)
		{
			var (preMatrix, _) = GetPreMatrix(table, origin, asRightHanded);
			var postMatrix = GetPostMatrix(table, origin);
			return new RenderObjectGroup(_data.Name, "Spinners", postMatrix, new RenderObject(
					name: "Plate",
					mesh: SpinnerPlateMesh.Clone().Transform(preMatrix),
					map: table.GetTexture(_data.Image),
					material: table.GetMaterial(_data.Material),
					isVisible: _data.IsVisible
				),
				new RenderObject(
					name:"Bracket",
					mesh: SpinnerBracketMesh.Clone().Transform(preMatrix),
					map: table.GetTexture(_data.Image),
					material: table.GetMaterial(_data.Material),
					isVisible: _data.IsVisible && _data.ShowBracket
				)
			);
		}

		protected override float BaseHeight(Table.Table table)
		{
			return table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
		}

		#region Mesh Imports

		private static readonly Mesh SpinnerPlateMesh = new Mesh("Plate", SpinnerPlate.Vertices, SpinnerPlate.Indices);
		private static readonly Mesh SpinnerBracketMesh = new Mesh("Bracket", SpinnerBracket.Vertices, SpinnerBracket.Indices);

		#endregion
	}
}
