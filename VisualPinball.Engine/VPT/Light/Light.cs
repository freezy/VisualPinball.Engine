using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;

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

		public Light(BinaryReader reader, string itemName) : this(new LightData(reader, itemName))
		{
		}

		public static Light GetDefault(Table.Table table)
		{
			var x = table.Width / 2f;
			var y = table.Height / 2f;
			var lightData = new LightData(table.GetNewName<Light>("Light"), table.Width / 2f, table.Height / 2f) {
				DragPoints = new[] {
					new DragPointData(x, y - 50f) {IsSmooth = true },
					new DragPointData(x - 50f * MathF.Cos(MathF.PI / 4), y - 50f * MathF.Sin(MathF.PI / 4)) {IsSmooth = true },
					new DragPointData(x - 50f, y) {IsSmooth = true },
					new DragPointData(x - 50f * MathF.Cos(MathF.PI / 4), y + 50f * MathF.Sin(MathF.PI / 4)) {IsSmooth = true },
					new DragPointData(x, y + 50f) {IsSmooth = true },
					new DragPointData(x + 50f * MathF.Cos(MathF.PI / 4), y + 50f * MathF.Sin(MathF.PI / 4)) {IsSmooth = true },
					new DragPointData(x + 50f, y) {IsSmooth = true },
					new DragPointData(x + 50f * MathF.Cos(MathF.PI / 4), y - 50f * MathF.Sin(MathF.PI / 4)) {IsSmooth = true },
				}
			};
			return new Light(lightData);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
