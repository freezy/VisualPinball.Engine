using System.IO;

namespace VisualPinball.Engine.VPT.Timer
{
	public class Timer : Item<TimerData>
	{
		public Timer(BinaryReader reader, string itemName) : base(new TimerData(reader, itemName))
		{
		}
	}
}
