using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Timer
{
	public class TimerHit
	{
		public uint NextFire;
		public int Interval;

		public TimerHit(uint nextFire, int interval)
		{
			NextFire = nextFire;
			Interval = interval;
		}
	}
}
