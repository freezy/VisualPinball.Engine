using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Timer
{
	public class TimerHit
	{
		public readonly EventProxy Events; // IFireEvents *m_pfe;
		public int NextFire;
		public int Interval;

		public TimerHit(EventProxy events, int nextFire, int interval)
		{
			Events = events;
			NextFire = nextFire;
			Interval = interval;
		}
	}
}
