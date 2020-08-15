using Unity.Entities;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	/// <summary>
	/// This struct passes an event triggered in a system to the main thread
	/// where it's dispatched to the API.
	/// </summary>
	public readonly struct EventData
	{
		public readonly EventId eventId;
		public readonly Entity ItemEntity;
		public readonly float FloatParam;
		public readonly bool GroupEvent;

		public EventData(EventId eventId, Entity itemEntity, bool groupEvent = false) : this()
		{
			this.eventId = eventId;
			ItemEntity = itemEntity;
			GroupEvent = groupEvent;
		}

		public EventData(EventId eventId, Entity itemEntity, float floatParam, bool groupEvent = false) : this()
		{
			this.eventId = eventId;
			ItemEntity = itemEntity;
			FloatParam = floatParam;
			GroupEvent = groupEvent;
		}
	}
}
