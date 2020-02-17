using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Timer
{
	public class TimerHit
	{
		public readonly EventProxy Events; // IFireEvents *m_pfe;
		public uint NextFire;
		public int Interval;

		public TimerHit(EventProxy events, uint nextFire, int interval)
		{
			Events = events;
			NextFire = nextFire;
			Interval = interval;
		}
	}
}
