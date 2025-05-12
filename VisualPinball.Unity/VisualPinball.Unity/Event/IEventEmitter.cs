using System;

namespace VisualPinball.Unity
{
	public interface IPinballEventEmitter
	{
		public PinballEvent[] Events { get; }
		public event EventHandler<PinballEventArgs> OnPinballEvent;
	}

	public readonly struct PinballEvent
	{
		public readonly string Name;
		public readonly PinballEventUnit Unit;

		public PinballEvent(string name, PinballEventUnit unit)
		{
			Name = name;
			Unit = unit;
		}
	}

	public readonly struct PinballEventArgs
	{
		public readonly string Name;
		public readonly float Value;
		public readonly PinballEventUnit Unit;

		public PinballEventArgs(string name, float value, PinballEventUnit unit)
		{
			Name = name;
			Value = value;
			Unit = unit;
		}
	}

	public enum PinballEventUnit
	{
		None,
		Percent,
		Degrees,
		DegreesPerSecond,
		Meters,
		MetersPerSecond,
		Radians,
		RadiansPerSecond,
	}
}
