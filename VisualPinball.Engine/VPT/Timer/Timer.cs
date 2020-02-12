using System.IO;

namespace VisualPinball.Engine.VPT.Timer
{
	public class Timer : Item<TimerData>
	{
		public Timer(TimerData data) : base(data)
		{
		}

		public Timer(BinaryReader reader, string itemName) : this(new TimerData(reader, itemName))
		{
		}
	}
}
