using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Light
{
	public class Light : Item<LightData>, IRenderable
	{
		public const string BulbMaterialName = "__bulbMaterial";
		public const string SocketMaterialName = "__bulbSocketMaterial";

		private readonly LightMeshGenerator _meshGenerator;

		public Light(LightData data) : base(data)
		{
			_meshGenerator = new LightMeshGenerator(Data);
		}

		public Light(BinaryReader reader, string itemName) : this(new LightData(reader, itemName)) { }

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
