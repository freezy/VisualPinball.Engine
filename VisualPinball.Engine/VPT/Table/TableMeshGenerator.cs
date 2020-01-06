using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Table
{
	public class TableMeshGenerator
	{
		private readonly TableData _data;

		public RenderObject Playfield { get; private set; }

		public TableMeshGenerator(TableData data)
		{
			_data = data;
		}

		public void SetFromPrimitive(Table table, Primitive.Primitive primitive)
		{
			Playfield = primitive.GetRenderObjects(table)[0];
		}

		public void SetFromTableDimensions(Table table)
		{
			var mesh = new Mesh {
				Vertices = new [] {
					new Vertex3DNoTex2(_data.Left, _data.Top, _data.TableHeight),
					new Vertex3DNoTex2(_data.Right, _data.Top, _data.TableHeight),
					new Vertex3DNoTex2(_data.Right, _data.Bottom, _data.TableHeight),
					new Vertex3DNoTex2(_data.Left, _data.Bottom, _data.TableHeight),
				},
				Indices = new [] { 0, 1, 3, 1, 2, 3 }
			};

			for (var i = 0; i < 4; ++i) {
				mesh.Vertices[i].Nx = 0;
				mesh.Vertices[i].Ny = 0;
				mesh.Vertices[i].Nz = 1.0f;

				mesh.Vertices[i].Tv = (i & 2) > 0 ? 1.0f : 0.0f;
				mesh.Vertices[i].Tu = (i == 1 || i == 2) ? 1.0f : 0.0f;
			}
			Playfield = new RenderObject(
				mesh: mesh,
				map: table.GetTexture(_data.Image),
				material: table.GetMaterial(_data.PlayfieldMaterial)
			);
		}
	}
}
