using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Light
{
	public class Light : Item<LightData>, IRenderable
	{
		public const string BulbMaterialName = "__bulbMaterial";
		public const string SocketMaterialName = "__bulbSocketMaterial";

		private readonly LightMeshGenerator _meshGenerator;

		public Light(BinaryReader reader, string itemName) : base(new LightData(reader, itemName))
		{
			_meshGenerator = new LightMeshGenerator(Data);
		}

		public RenderObject[] GetRenderObjects(Table.Table table, Origin origin)
		{
			return _meshGenerator.GetRenderObjects(table, origin);
		}
	}
}
