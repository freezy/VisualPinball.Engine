using System.IO;

namespace VisualPinball.Engine.VPT.DispReel
{
	public class DispReel : Item<DispReelData>
	{
		public DispReel(BinaryReader reader, string itemName) : base(new DispReelData(reader, itemName))
		{
		}
	}
}
