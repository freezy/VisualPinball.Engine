using System.IO;

namespace VisualPinball.Engine.VPT.Sound
{
	public class Sound : Item<SoundData>
	{
		public Sound(BinaryReader reader, string itemName) : base(new SoundData(reader, itemName))
		{
		}
	}
}
