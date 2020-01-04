using System.IO;

namespace VisualPinball.Engine.VPT.Bumper
{
	public class Bumper : Item<BumperData>
	{
		private readonly BumperMeshGenerator _meshGenerator;

		public Bumper(BinaryReader reader, string itemName) : base(new BumperData(reader, itemName))
		{
			_meshGenerator = new BumperMeshGenerator(Data);
		}
	}
}
