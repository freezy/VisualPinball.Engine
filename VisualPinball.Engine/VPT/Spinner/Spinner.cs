using System.IO;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Spinner
{
	public class Spinner : Item<SpinnerData>, IRenderable
	{
		private readonly SpinnerMeshGenerator _meshGenerator;

		public Spinner(BinaryReader reader, string itemName) : base(new SpinnerData(reader, itemName))
		{
			_meshGenerator = new SpinnerMeshGenerator(Data);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}
	}
}
