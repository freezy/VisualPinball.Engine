namespace VisualPinball.Engine.VPT
{
	public interface ITimer
	{
		bool IsTimerEnabled { get; set; }
		int TimerInterval { get; set; }
	}
}
