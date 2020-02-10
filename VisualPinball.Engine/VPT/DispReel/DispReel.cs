using System.IO;

namespace VisualPinball.Engine.VPT.DispReel
{
	public class DispReel : Item<DispReelData>
	{
		public DispReel(DispReelData data) : base(data)
		{
		}

		public DispReel(BinaryReader reader, string itemName) : this(new DispReelData(reader, itemName))
		{
		}
	}
}
