namespace VisualPinball.Engine.VPT.Timer
{
	public class TimerOnOff
	{
		public bool Enabled;
		public TimerHit Timer;

		public TimerOnOff(bool enabled, TimerHit timer)
		{
			Enabled = enabled;
			Timer = timer;
		}
	}
}
